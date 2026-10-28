using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using Phloem.Models;
using Phloem.Services;
using Phloem.Services.Api;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;
using Phloem.Settings;

namespace Phloem.ViewModels;

/// <summary>One dish on the seller's order page.</summary>
public sealed class ShopOrderLine
{
    public required OrderLineDto Line { get; init; }
    public required string CurrencySymbol { get; init; }
    public string? ImageUrl { get; init; }
    public string Name => Line.DishName;
    public string QuantityText => $"×{Line.Quantity}";
    public string LineTotalText => $"{CurrencySymbol}{Line.LineTotal:N2}";
}

/// <summary>
/// The seller's view of one order (route "shoporder", parameter "OrderNumber"): who it's for, what they ordered,
/// when and where to deliver, then the next step:
/// <list type="bullet">
///   <item><description><b>Waiting</b> (Paid): Accept, or Decline with a reason (cancelled and refunded; the customer
///   is emailed the reason).</description></item>
///   <item><description><b>Being prepared</b>: Send out, choosing a courier (or "I'll deliver it myself").</description></item>
///   <item><description><b>Out for delivery</b>: Change courier; when the seller is delivering it themselves,
///   "Delivered · take a photo" (the courier's photo screen).</description></item>
///   <item><description><b>Delivered</b>: the proof-of-delivery photo.</description></item>
/// </list>
/// </summary>
public partial class ShopOrderDetailsViewModel : ObservableObject, IQueryAttributable
{
    /// <summary>Most characters in a decline reason (the API's limit).</summary>
    public const int DeclineReasonMaxLength = 500;

    /// <summary>Ready-made reasons offered first; "Other reason…" lets the seller type one.</summary>
    private static readonly string[] QuickReasons =
    [
        "Sorry, we've run out of one of the dishes.",
        "Sorry, we're too busy to make it in time.",
        "Sorry, we can't deliver to this address."
    ];

    private const string OtherReason = "Other reason…";

    private readonly SellerService _seller;
    private readonly ApiSettings _api;
    private string _orderNumber = string.Empty;
    private bool _busy;

    public ShopOrderDetailsViewModel(SellerService seller, IOptions<ApiSettings> api)
    {
        _seller = seller;
        _api = api.Value;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOrder), nameof(Title), nameof(StatusText), nameof(IsWaiting),
                              nameof(IsPreparing), nameof(IsOutForDelivery), nameof(IsCancelled), nameof(WhenText),
                              nameof(AddressText), nameof(RecipientText), nameof(PhoneText), nameof(HasPhone),
                              nameof(SubtotalText), nameof(DeliveryFeeText), nameof(TotalText),
                              nameof(ItemCountText), nameof(CancelledText), nameof(CourierText), nameof(HasCourier),
                              nameof(CanMarkDelivered), nameof(Proof))]
    public partial OrderDetailsDto? Order { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<ShopOrderLine> Lines { get; set; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsError { get; set; }

    public bool HasMessage => Message.Length > 0;
    public bool HasOrder => Order is not null;

    private string Symbol => Order is null ? string.Empty : OrderService.SymbolOf(Order.Currency);

    public string Title => Order is null ? _orderNumber : $"{Order.OrderNumber} · {Order.CustomerName}";

    public string StatusText => Order?.Status switch
    {
        OrderStatus.Paid => "Waiting for you",
        OrderStatus.Preparing => "Being prepared",
        OrderStatus.OutForDelivery => "Out for delivery",
        OrderStatus.Delivered => "Delivered",
        OrderStatus.Cancelled => Order?.DeclinedByShop == true ? "Declined" : "Cancelled by customer",
        null => string.Empty,
        var other => other.ToString()!
    };

    public bool IsWaiting => Order?.Status == OrderStatus.Paid;
    public bool IsPreparing => Order?.Status == OrderStatus.Preparing;
    public bool IsOutForDelivery => Order?.Status == OrderStatus.OutForDelivery;
    public bool IsCancelled => Order?.Status == OrderStatus.Cancelled;

    /// <summary>"Paid 14:05 · Deliver: As soon as possible".</summary>
    public string WhenText => Order is null
        ? string.Empty
        : (Order.PaidAt is { } paid ? $"Paid {paid.ToLocalTime():d MMM, HH:mm} · " : string.Empty) +
          $"Deliver: {DeliveryTimeOption.TextOf(Order.DeliveryWindow)}";

    public string AddressText => Order is null ? string.Empty
        : string.IsNullOrWhiteSpace(Order.DeliveryLabel) ? Order.DeliveryAddress
        : $"{Order.DeliveryLabel} · {Order.DeliveryAddress}";

    /// <summary>Who receives it: the address's name, else the customer.</summary>
    public string RecipientText => Order?.DeliveryRecipientName ?? Order?.CustomerName ?? string.Empty;

    /// <summary>The number to call about the delivery; empty when there's none.</summary>
    public string PhoneText => Order?.DeliveryPhone ?? string.Empty;
    public bool HasPhone => !string.IsNullOrWhiteSpace(Order?.DeliveryPhone);

    /// <summary>Tap the phone number: opens the phone app with it.</summary>
    [RelayCommand]
    private async Task Call()
    {
        if (!HasPhone) return;
        try { await Launcher.Default.OpenAsync(new Uri($"tel:{Order!.DeliveryPhone}")); }
        catch (Exception) { await Shell.Current.DisplayAlertAsync("Can't call from here", Order!.DeliveryPhone!, "OK"); }
    }

    public string ItemCountText => Order is null ? string.Empty : $"{Order.Lines.Sum(l => l.Quantity)} item(s)";
    public string SubtotalText => Order is null ? string.Empty : $"{Symbol}{Order.Subtotal:N2}";
    public string DeliveryFeeText => Order is null ? string.Empty : $"{Symbol}{Order.DeliveryFee:N2}";
    public string TotalText => Order is null ? string.Empty : $"{Symbol}{Order.Total:N2}";

    /// <summary>"Kwame Asante is delivering it" / "You're delivering it" / "Delivered by …".</summary>
    public string CourierText => Order switch
    {
        { CourierName: null } => string.Empty,
        { CanMarkDelivered: true } => "🛵  You're delivering it yourself",
        { Status: OrderStatus.Delivered } => $"🛵  Delivered by {Order.CourierName}",
        _ => $"🛵  {Order.CourierName} is delivering it"
    };

    public bool HasCourier => CourierText.Length > 0;

    /// <summary>The seller is this order's courier and it's out for delivery: show "Delivered · take a photo".</summary>
    public bool CanMarkDelivered => Order?.CanMarkDelivered == true;

    /// <summary>The courier's photo once delivered; null before.</summary>
    public DeliveryProofView? Proof => DeliveryProofView.From(Order?.DeliveryProof, _api);

    public string CancelledText => Order is not { Status: OrderStatus.Cancelled }
        ? string.Empty
        : Order.DeclinedByShop
            ? $"You declined this order: \"{Order.DeclineReason}\". The customer gets a full refund."
            : "The customer cancelled this order. They get a full refund.";

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("OrderNumber", out var value) && value is string number)
        {
            _orderNumber = number;
            Order = null;
            Lines = [];
        }
    }

    /// <summary>Called each time the page appears.</summary>
    public async Task LoadAsync()
    {
        if (_orderNumber.Length == 0) return;
        IsLoading = true;
        try
        {
            var result = await _seller.GetOrderAsync(_orderNumber);
            if (result.IsFailure)
            {
                ShowError(result.Error!.DisplayMessage);
                return;
            }

            Order = result.Value;
            var symbol = Symbol;
            Lines = Order!.Lines
                .Select(l => new ShopOrderLine { Line = l, CurrencySymbol = symbol, ImageUrl = _api.ResolveFileUrl(l.ImageUrl) })
                .ToList();
            if (IsError) Message = string.Empty;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Accept() =>
        await RunAsync(o => _seller.AcceptAsync(o), "Accepted. It's in \"Being prepared\".");

    /// <summary>Send out: who's taking it? (me, or a courier in my country). The courier is emailed.</summary>
    [RelayCommand]
    private async Task SendOut()
    {
        var courier = await ChooseCourierAsync("Who's delivering it?");
        if (courier is null) return;

        await RunAsync(o => _seller.SendOutAsync(o, courier.IsMe ? null : courier.Id),
                       courier.IsMe
                           ? "On its way. When you hand it over, tap \"Delivered\" and take a photo."
                           : $"On its way with {courier.Name}. They'll take a photo when they deliver it.");
    }

    /// <summary>Give it to another courier (or take it back myself), e.g. the first one can't make it.</summary>
    [RelayCommand]
    private async Task ChangeCourier()
    {
        var courier = await ChooseCourierAsync("Who's delivering it now?");
        if (courier is null) return;

        await RunAsync(o => _seller.ChangeCourierAsync(o, courier.IsMe ? null : courier.Id),
                       courier.IsMe ? "You're delivering it now." : $"{courier.Name} is delivering it now.");
    }

    /// <summary>I'm delivering it: the photo screen (a photo is needed to confirm delivery).</summary>
    [RelayCommand]
    private async Task MarkDelivered()
    {
        if (Order is null || !CanMarkDelivered) return;
        await Shell.Current.GoToAsync("deliveryproof", new Dictionary<string, object>
        {
            ["Kind"] = DeliveryKind.Order,
            ["Reference"] = Order.OrderNumber,
            ["Recipient"] = RecipientText
        });
    }

    /// <summary>The choices from the API ("I'll deliver it myself" first); null when cancelled.</summary>
    private async Task<CourierOptionDto?> ChooseCourierAsync(string title)
    {
        var result = await _seller.GetCouriersAsync();
        if (result.IsFailure)
        {
            ShowError(result.Error!.DisplayMessage);
            return null;
        }

        var options = result.Value!;
        var labels = options.Select(c => c.IsMe ? "🙋  I'll deliver it myself" : $"🛵  {c.Name}").ToArray();
        var picked = await Shell.Current.DisplayActionSheetAsync(
            options.Count == 1 ? $"{title}\n(No couriers in your country yet.)" : title, "Cancel", null, labels);

        var index = Array.IndexOf(labels, picked);
        return index < 0 ? null : options[index];
    }

    /// <summary>Decline: pick a reason (or type one); the customer is refunded and emailed it.</summary>
    [RelayCommand]
    private async Task Decline()
    {
        if (Order is null) return;

        var choice = await Shell.Current.DisplayActionSheetAsync("Why can't you make this order?", "Keep the order",
                                                                 null, [.. QuickReasons, OtherReason]);
        if (choice is null || choice == "Keep the order") return;

        var reason = choice;
        if (choice == OtherReason)
        {
            reason = await Shell.Current.DisplayPromptAsync("Decline order",
                "Tell the customer why. They'll get a full refund.", "Decline", "Keep the order",
                "e.g. We've run out of tilapia today", DeclineReasonMaxLength);
            if (string.IsNullOrWhiteSpace(reason)) return;
        }

        var ok = await Shell.Current.DisplayAlertAsync("Decline this order?",
            $"{Order.CustomerName} gets a full refund and this message:\n\n\"{reason.Trim()}\"",
            "Decline order", "Keep the order");
        if (ok) await RunAsync(o => _seller.DeclineAsync(o, reason.Trim()), "Declined. The customer has been told.");
    }

    [RelayCommand]
    private async Task Refresh() => await LoadAsync();

    /// <summary>Runs one step, then shows the order as the server has it now.</summary>
    private async Task RunAsync(Func<string, Task<ApiResult>> step, string done)
    {
        if (_busy || Order is null) return;
        _busy = true;
        try
        {
            var result = await step(Order.OrderNumber);
            if (result.IsFailure)
            {
                ShowError(result.Error!.DisplayMessage);
                await LoadAsync();   // it may have moved on (e.g. the customer cancelled)
                return;
            }

            await LoadAsync();
            IsError = false;
            Message = done;
        }
        finally
        {
            _busy = false;
        }
    }

    private void ShowError(string message)
    {
        IsError = true;
        Message = message;
    }
}
