using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Options;
using Phloem.Models;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;
using Phloem.Settings;

namespace Phloem.Services;

/// <summary>One dish + quantity inside a placed order (used by "Buy again" and Order details).</summary>
public record OrderLine(Dish Dish, int Quantity)
{
    /// <summary>Price at the time of ordering (so later price changes don't alter old orders).</summary>
    public decimal UnitPrice { get; init; } = Dish.Price;

    /// <summary>The order's currency symbol, e.g. "GH₵".</summary>
    public string CurrencySymbol { get; init; } = Dish.CurrencySymbol;

    public decimal LineTotal => UnitPrice * Quantity;
    public string LineTotalText => $"{CurrencySymbol}{LineTotal:N2}";
    public string QuantityText => $"×{Quantity}";
}

/// <summary>A placed order, shown under "Recent orders" on the Orders page and on Order details.</summary>
/// <remarks>Orders come from the API (<see cref="OrderHistoryService.LoadAsync"/>). Parcels ("Send Food to UK") are
/// separate bookings: see <c>ShipmentService</c>.</remarks>
public partial class PlacedOrder : ObservableObject
{
    /// <summary>How long after paying the customer can still cancel (the API's rule).</summary>
    public static readonly TimeSpan CancelWindow = TimeSpan.FromMinutes(30);

    public required string OrderNumber { get; init; }
    public DateTime PlacedAt { get; init; } = DateTime.Now;

    /// <summary>The shop the order is from.</summary>
    public string ShopName { get; init; } = string.Empty;

    /// <summary>A picture of one of the dishes (for the list), or null.</summary>
    public string? ImageUrl { get; init; }

    /// <summary>e.g. "Jollof Rice ×2, Egusi Soup ×1"</summary>
    public string ItemsSummary { get; init; } = string.Empty;
    public int ItemCount { get; init; }
    public decimal Subtotal { get; init; }
    public decimal DeliveryFee { get; init; }
    public decimal Total { get; init; }

    /// <summary>"Home · 31 Camden High St, London".</summary>
    public string DeliveryAddress { get; init; } = string.Empty;
    public string CurrencySymbol { get; init; } = "£";
    public string PaymentMethod { get; init; } = string.Empty;

    /// <summary>The dishes and quantities (filled on Order details; empty in the list).</summary>
    public IReadOnlyList<OrderLine> Lines { get; init; } = Array.Empty<OrderLine>();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCancelled), nameof(CanCancel), nameof(StatusText), nameof(IsPreparing),
                              nameof(IsOnItsWay), nameof(ThirdStepTitle), nameof(ThirdStepText))]
    public partial OrderStatus Status { get; set; } = OrderStatus.Paid;

    /// <summary>Who is bringing it (or brought it); null until the shop sends it out.</summary>
    public string? CourierName { get; init; }

    /// <summary>The courier's photo once delivered (Order details only); null before.</summary>
    public DeliveryProofView? Proof { get; init; }

    /// <summary>The API's answer to "can it still be cancelled?" when the order was loaded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCancel), nameof(StatusText))]
    public partial bool AllowsCancel { get; set; } = true;

    /// <summary>When cancelling stops being possible; null = no time limit (e.g. not paid yet).</summary>
    public DateTime? CancelDeadline { get; init; }

    public bool IsCancelled => Status == OrderStatus.Cancelled;

    /// <summary>True while the order can still be cancelled (the API has the final say).</summary>
    public bool CanCancel => !IsCancelled && AllowsCancel && (CancelDeadline is null || DateTime.Now < CancelDeadline);

    public string StatusText => Status switch
    {
        OrderStatus.AwaitingPayment => "Awaiting payment",
        OrderStatus.Paid => "Order placed",
        OrderStatus.Preparing => "Being prepared",
        OrderStatus.OutForDelivery => "On its way",
        OrderStatus.Delivered => "Delivered",
        OrderStatus.Cancelled => "Cancelled",
        _ => Status.ToString()
    };

    public string TotalText => $"{CurrencySymbol}{Total:N2}";
    public string SubtotalText => $"{CurrencySymbol}{Subtotal:N2}";
    public string DeliveryFeeText => $"{CurrencySymbol}{DeliveryFee:N2}";

    /// <summary>The shop's reason when IT declined the order (shown on Order details); null otherwise.</summary>
    public string? DeclineReason { get; init; }

    public bool WasDeclined => DeclineReason is not null;

    /// <summary>"Mama Ama's Kitchen couldn't make this order: …"</summary>
    public string DeclineText => $"{ShopName} couldn't make this order: \"{DeclineReason}\"";

    /// <summary>When the order was cancelled (shown on Order details).</summary>
    [ObservableProperty]
    public partial DateTime? CancelledAt { get; set; }

    // ---------- Progress tracker (Order details) ----------
    /// <summary>Step 2 done once the shop has started.</summary>
    public bool IsPreparing => Status is OrderStatus.Preparing or OrderStatus.OutForDelivery or OrderStatus.Delivered;
    public string PlacedAtText => PlacedAt.ToString("d MMM yyyy, HH:mm");

    /// <summary>Step 3 done once it has left the shop.</summary>
    public bool IsOnItsWay => Status is OrderStatus.OutForDelivery or OrderStatus.Delivered;

    public string ThirdStepTitle => Status == OrderStatus.Delivered ? "Delivered" : "On its way";

    /// <summary>"Kwame is bringing it", "Left at the front door", or what happens next.</summary>
    public string ThirdStepText => Status switch
    {
        OrderStatus.Delivered => Proof?.Summary ?? "Enjoy your meal!",
        OrderStatus.OutForDelivery => CourierName is null ? "On its way to you" : $"{CourierName} is bringing it",
        _ => "We'll let you know when it's on the move"
    };

    /// <summary>Re-check the time-based cancel window (called when the Orders page refreshes).</summary>
    public void RefreshTiming()
    {
        OnPropertyChanged(nameof(CanCancel));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsPreparing));
        OnPropertyChanged(nameof(IsOnItsWay));
    }
}

/// <summary>
/// "My orders": the signed-in user's orders from the API (<see cref="Orders"/>), plus Cancel and Buy again.
/// Emptied when the user signs out and reloaded when they sign in. Register as a singleton.
/// </summary>
public class OrderHistoryService
{
    private readonly OrderService _orders;
    private readonly SessionService _session;
    private readonly ApiSettings _api;
    private Task? _loading;

    public ObservableCollection<PlacedOrder> Orders { get; } = new();

    /// <summary>The page of "My orders" being shown (the Orders page's pager sets it).</summary>
    public int Page { get; private set; } = 1;

    /// <summary>Orders per page (the Orders page's pager sets it).</summary>
    public int PageSize { get; private set; } = 10;

    /// <summary>All my orders on the server (for the pager).</summary>
    public int TotalCount { get; private set; }

    public OrderHistoryService(OrderService orders, SessionService session, IOptions<ApiSettings> api)
    {
        _orders = orders;
        _session = session;
        _api = api.Value;

        // Signed out → nobody else on this phone sees the orders; signed in → load theirs
        session.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (!session.IsLoggedIn)
            {
                Orders.Clear();
                return;
            }

            try { await LoadAsync(); }
            catch (Exception) { /* the Orders page loads them again when it opens */ }
        });
    }

    // ───────────────────────────── 1. Loading ─────────────────────────────

    /// <summary>
    /// Reloads "My orders" from the API (several calls at once share one load). Without a page: page 1 (newest
    /// first, e.g. after placing an order).
    /// </summary>
    /// <param name="page">1-based page; null = page 1.</param>
    /// <param name="pageSize">Orders per page; null = as before.</param>
    /// <returns>Null when loaded (or signed out); otherwise why it failed.</returns>
    public async Task<string?> LoadAsync(int? page = null, int? pageSize = null)
    {
        if (!_session.IsLoggedIn)
        {
            Orders.Clear();
            TotalCount = 0;
            return null;
        }

        Page = Math.Max(1, page ?? 1);
        if (pageSize is > 0) PageSize = pageSize.Value;

        string? error = null;
        _loading ??= LoadCoreAsync(e => error = e);
        try { await _loading; }
        finally { _loading = null; }
        return error;
    }

    private async Task LoadCoreAsync(Action<string> fail)
    {
        var result = await _orders.GetMyOrdersAsync(Page, PageSize);
        if (result.IsFailure)
        {
            fail(result.Error!.DisplayMessage);
            return;
        }

        TotalCount = result.Value!.TotalCount;
        Orders.Clear();
        foreach (var order in result.Value!.Items.Select(ToPlacedOrder))
            Orders.Add(order);
    }

    /// <summary>The full order (with its dishes) for Order details; null with a message when it can't be loaded.</summary>
    public async Task<(PlacedOrder? Order, string? Error)> LoadDetailsAsync(string orderNumber)
    {
        var result = await _orders.GetAsync(orderNumber);
        return result.IsFailure ? (null, result.Error!.DisplayMessage) : (ToPlacedOrder(result.Value!), null);
    }

    // ───────────────────────────── 2. Cancel ─────────────────────────────

    /// <summary>Cancel order (shared by the Orders list and Order details): asks first, then cancels on the API.</summary>
    public async Task<bool> CancelAsync(PlacedOrder order)
    {
        if (!order.CanCancel)
        {
            await Shell.Current.DisplayAlertAsync("Can't cancel",
                "This order is already being prepared, so it can no longer be cancelled.", "OK");
            order.RefreshTiming();
            return false;
        }

        var refund = order.Status == OrderStatus.AwaitingPayment
            ? "Nothing has been paid for it yet."
            : "Your payment will be refunded.";
        var confirm = await Shell.Current.DisplayAlertAsync("Cancel this order?",
            $"{order.OrderNumber} · {order.TotalText}\n{refund}", "Cancel order", "Keep order");
        if (!confirm) return false;

        var result = await _orders.CancelAsync(order.OrderNumber);
        if (result.IsFailure)
        {
            await Shell.Current.DisplayAlertAsync("Can't cancel", result.Error!.DisplayMessage, "OK");
            await LoadAsync();   // show what the order's status really is now
            return false;
        }

        order.Status = OrderStatus.Cancelled;
        order.AllowsCancel = false;
        order.CancelledAt = DateTime.Now;
        return true;
    }

    // ───────────────────────────── 3. Buy again ─────────────────────────────

    /// <summary>Buy again: put the same dishes and quantities back in the basket, then offer Checkout.</summary>
    public async Task BuyAgainAsync(PlacedOrder order, CartService cart)
    {
        var (lines, unavailable) = await LinesToBuyAgainAsync(order.OrderNumber);

        if (lines is null) return;   // the error was already shown

        foreach (var line in lines)
            for (var i = 0; i < line.Quantity; i++)
                cart.AddDish(line.Dish);

        var count = lines.Sum(l => l.Quantity);
        var message = count == 0
            ? "None of these dishes are on sale right now."
            : $"{count} item(s) from {order.OrderNumber} are back in your basket.";
        if (unavailable.Count > 0)
            message += $"\nNo longer available: {string.Join(", ", unavailable)}.";

        if (count == 0)
        {
            await Shell.Current.DisplayAlertAsync("Buy again", message, "OK");
            return;
        }

        var go = await Shell.Current.DisplayAlertAsync("Added to your basket", message, "Checkout", "Keep shopping");
        if (go)
            await AppNav.GoToCheckoutAsync();
    }

    /// <summary>The dishes still on sale (from the API), with their names and pictures from the order.</summary>
    private async Task<(IReadOnlyList<OrderLine>? Lines, IReadOnlyList<string> Unavailable)> LinesToBuyAgainAsync(
        string orderNumber)
    {
        var again = await _orders.BuyAgainAsync(orderNumber);
        var details = again.IsSuccess ? await _orders.GetAsync(orderNumber) : null;
        var error = again.IsFailure ? again.Error : details?.IsFailure == true ? details.Error : null;
        if (error is not null)
        {
            await Shell.Current.DisplayAlertAsync("Buy again", error.DisplayMessage, "OK");
            return (null, []);
        }

        var order = details!.Value!;
        var symbol = OrderService.SymbolOf(order.Currency);
        var lines = again.Value!.Items
            .Select(item => (Item: item, Line: order.Lines.FirstOrDefault(l => l.DishId == item.DishId)))
            .Where(x => x.Line is not null)
            .Select(x => new OrderLine(ToDish(x.Line!, order.ShopName, symbol), x.Item.Quantity))
            .ToList();
        return (lines, again.Value.UnavailableDishes);
    }

    // ───────────────────────────── 4. API → app models ─────────────────────────────

    private PlacedOrder ToPlacedOrder(OrderSummaryDto o) => new()
    {
        OrderNumber = o.OrderNumber,
        ShopName = o.ShopName,
        ImageUrl = _api.ResolveFileUrl(o.ImageUrl),
        PlacedAt = o.PlacedAt.ToLocalTime(),
        ItemsSummary = o.ItemsSummary,
        ItemCount = o.ItemCount,
        Total = o.Total,
        CurrencySymbol = OrderService.SymbolOf(o.Currency),
        PaymentMethod = PaymentText(o.Status),
        Status = o.Status,
        AllowsCancel = o.CanCancel,
        CancelDeadline = o.Status == OrderStatus.Paid && o.PaidAt is { } paid ? paid.ToLocalTime() + PlacedOrder.CancelWindow : null
    };

    private PlacedOrder ToPlacedOrder(OrderDetailsDto o)
    {
        var symbol = OrderService.SymbolOf(o.Currency);
        return new PlacedOrder
        {
            OrderNumber = o.OrderNumber,
            ShopName = o.ShopName,
            ImageUrl = _api.ResolveFileUrl(o.Lines.FirstOrDefault(l => l.ImageUrl is not null)?.ImageUrl),
            PlacedAt = o.PlacedAt.ToLocalTime(),
            ItemsSummary = string.Join(", ", o.Lines.Select(l => $"{l.DishName} ×{l.Quantity}")),
            ItemCount = o.Lines.Sum(l => l.Quantity),
            Subtotal = o.Subtotal,
            DeliveryFee = o.DeliveryFee,
            Total = o.Total,
            DeliveryAddress = string.IsNullOrWhiteSpace(o.DeliveryLabel)
                ? o.DeliveryAddress
                : $"{o.DeliveryLabel} · {o.DeliveryAddress}",
            CurrencySymbol = symbol,
            PaymentMethod = PaymentText(o.Status),
            Lines = o.Lines.Select(l => new OrderLine(ToDish(l, o.ShopName, symbol), l.Quantity)).ToList(),
            Status = o.Status,
            AllowsCancel = o.CanCancel,
            CancelDeadline = o.CancelSecondsLeft is { } seconds ? DateTime.Now.AddSeconds(seconds) : null,
            CancelledAt = o.CancelledAt?.ToLocalTime(),
            DeclineReason = o.DeclinedByShop ? o.DeclineReason ?? string.Empty : null,
            CourierName = o.CourierName,
            Proof = DeliveryProofView.From(o.DeliveryProof, _api)
        };
    }

    private Dish ToDish(OrderLineDto line, string shopName, string symbol) => new()
    {
        Id = line.DishId.ToString(),
        Name = line.DishName,
        ImageUrl = _api.ResolveFileUrl(line.ImageUrl) ?? string.Empty,
        Price = line.UnitPrice,
        VendorName = shopName,
        CurrencySymbol = symbol,
        IsAvailable = !line.IsOutOfStock
    };

    /// <summary>What the order list says about payment (only test payments exist for now).</summary>
    private static string PaymentText(OrderStatus status) => status switch
    {
        OrderStatus.AwaitingPayment => "Not paid yet",
        OrderStatus.Cancelled => "Cancelled",
        _ => "Test payment"
    };
}
