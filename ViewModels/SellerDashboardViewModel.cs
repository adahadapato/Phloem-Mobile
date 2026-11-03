using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Models;
using Phloem.Services;
using Phloem.Services.Api;
using Phloem.Services.Api.Contracts;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;
using Microsoft.Extensions.Options;
using Phloem.Settings;

namespace Phloem.ViewModels;

/// <summary>One bar in the "Sales overview" chart.</summary>
public record SalesBar(string Day, double BarHeight, bool IsWeekend);

/// <summary>
/// Seller dashboard (route "dashboard"). From the API:
/// <list type="number">
///   <item><description><b>Tiles:</b> balance, total orders, orders waiting, store rating.</description></item>
///   <item><description><b>Orders waiting:</b> paid orders to accept or decline. Tapping one opens it (what the
///   customer ordered) before deciding. "No orders waiting right now." when there are none.</description></item>
///   <item><description><b>Being prepared</b> and <b>Out for delivery</b>: tap to move them along.</description></item>
///   <item><description><b>Sales overview:</b> orders per day this week.</description></item>
/// </list>
/// "My dishes" (from the API, a page at a time): tap to edit, − / + to change the price within its allowed range.
/// </summary>
public partial class SellerDashboardViewModel : ObservableObject
{
    private readonly ApiSettings _api;

    /// <summary>All my dishes from the server; <see cref="MyDishes"/> shows one page of them.</summary>
    private IReadOnlyList<MyDishDto> _allDishes = [];
    private readonly SellerService _seller;
    private CancellationTokenSource? _load;

    /// <summary>The page of my dishes being shown.</summary>
    public ObservableCollection<Dish> MyDishes { get; } = new();

    /// <summary>Pages of "My dishes".</summary>
    public Paging DishesPager { get; }

    /// <summary>"2 dishes need a new price" (the admin changed their allowed range); empty when none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPriceUpdates))]
    public partial string PriceUpdateText { get; set; } = string.Empty;

    public bool HasPriceUpdates => PriceUpdateText.Length > 0;

    /// <summary>Mon–Sun bars for the sales chart (weekend bars show in orange).</summary>
    public ObservableCollection<SalesBar> WeeklySales { get; } = new();

    /// <summary>Paid orders waiting to be accepted or declined.</summary>
    public ObservableCollection<ShopOrderCard> WaitingOrders { get; } = new();

    /// <summary>Accepted orders in the kitchen.</summary>
    public ObservableCollection<ShopOrderCard> PreparingOrders { get; } = new();

    /// <summary>Orders on their way to the customer.</summary>
    public ObservableCollection<ShopOrderCard> OutForDeliveryOrders { get; } = new();

    /// <summary>Pages of each order list.</summary>
    public Paging WaitingPager { get; }
    public Paging PreparingPager { get; }
    public Paging OutForDeliveryPager { get; }

    /// <summary>My shop from the API (null until loaded, or when I have none).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Greeting), nameof(BalanceText), nameof(RatingText))]
    public partial MyShopDto? Shop { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TotalOrdersText), nameof(WaitingCountText))]
    public partial ShopOrderStatsDto? Stats { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>Why the dashboard couldn't load (empty when it did).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string Message { get; set; } = string.Empty;

    public bool HasMessage => Message.Length > 0;

    /// <summary>"Good morning, Mama Ama's Kitchen!" etc., based on the time of day.</summary>
    public string Greeting
    {
        get
        {
            var hour = DateTime.Now.Hour;
            var part = hour < 12 ? "Good morning" : hour < 17 ? "Good afternoon" : "Good evening";
            var name = Shop?.Name;
            return name is null ? $"{part}!" : $"{part}, {name}!";
        }
    }

    public string BalanceText => Shop is null ? "–" : $"{OrderService.SymbolOf(Shop.Currency)}{Shop.Balance:#,0.##}";
    public string RatingText => Shop is null || Shop.RatingCount == 0 ? "New" : $"{Shop.Rating:0.0}";
    public string TotalOrdersText => Stats?.TotalOrders.ToString() ?? "–";
    public string WaitingCountText => Stats is null ? "–" : $"{Stats.NewOrders:00}";

    public string WaitingTitle => $"Orders waiting ({WaitingPager.TotalCount})";
    public bool HasNoWaitingOrders => WaitingOrders.Count == 0;
    public string PreparingTitle => $"Being prepared ({PreparingPager.TotalCount})";
    public bool HasPreparingOrders => PreparingOrders.Count > 0;
    public string OutForDeliveryTitle => $"Out for delivery ({OutForDeliveryPager.TotalCount})";
    public bool HasOutForDeliveryOrders => OutForDeliveryOrders.Count > 0;

    public SellerDashboardViewModel(SellerService seller, IOptions<ApiSettings> api,
                                    PagingFactory paging)
    {
        _seller = seller;
        _api = api.Value;
        DishesPager = paging.Create(PagedLists.SellerDishes, () =>
        {
            ShowDishesPage();
            return Task.CompletedTask;
        });
        WaitingPager = paging.Create(PagedLists.SellerOrders,
            () => LoadOrderPageAsync(OrderStatus.Paid, WaitingOrders, WaitingPager));
        PreparingPager = paging.Create(PagedLists.SellerOrders,
            () => LoadOrderPageAsync(OrderStatus.Preparing, PreparingOrders, PreparingPager));
        OutForDeliveryPager = paging.Create(PagedLists.SellerOrders,
            () => LoadOrderPageAsync(OrderStatus.OutForDelivery, OutForDeliveryOrders, OutForDeliveryPager));
    }

    /// <summary>True when the shop has no dishes yet (shows the "Add your first dish" card).</summary>
    public bool HasNoDishes => _allDishes.Count == 0;

    /// <summary>My dishes from the server (those needing a new price first).</summary>
    private async Task LoadMyDishesAsync(CancellationToken ct)
    {
        var result = await _seller.GetMyDishesAsync(ct);
        if (result.IsFailure) return;

        _allDishes = result.Value!.Dishes;
        var needs = result.Value.NeedsPriceUpdateCount;
        PriceUpdateText = needs switch
        {
            0 => string.Empty,
            1 => "1 dish needs a new price: Phloem changed its allowed range. Tap it to update.",
            _ => $"{needs} dishes need a new price: Phloem changed their allowed range. Tap one to update."
        };

        DishesPager.Update(_allDishes.Count);
        if (DishesPager.Page > DishesPager.TotalPages) DishesPager.Reset();
        ShowDishesPage();
    }

    /// <summary>The pager's page of my dishes (all were loaded at once; the server doesn't page them).</summary>
    private void ShowDishesPage()
    {
        MyDishes.Clear();
        foreach (var dish in _allDishes.Skip((DishesPager.Page - 1) * DishesPager.PageSize).Take(DishesPager.PageSize))
            MyDishes.Add(ToDish(dish));
        OnPropertyChanged(nameof(HasNoDishes));
    }

    private Dish ToDish(MyDishDto d) => new()
    {
        Id = d.Id.ToString(),
        Name = d.Name,
        CatalogName = d.CatalogName,
        ImageUrl = _api.ResolveFileUrl(d.Photos.FirstOrDefault()?.ThumbnailUrl ?? d.Photos.FirstOrDefault()?.Url)
                   ?? string.Empty,
        Price = d.Price,
        CurrencySymbol = OrderService.SymbolOf(d.Currency),
        Rating = (double)d.Rating,
        Category = DishCatalogService.CategoryText(d.Category),
        VendorName = Shop?.Name ?? string.Empty,
        VendorId = Shop?.Id.ToString() ?? string.Empty,
        Description = d.Description ?? string.Empty,
        IsAvailable = d.IsAvailable,
        ReviewText = d.ReviewStatus switch
        {
            ContentReviewStatus.Checking => "Being checked",
            ContentReviewStatus.WithStaff => "Being checked by our team",
            ContentReviewStatus.Rejected => $"Not allowed: {d.ReviewNote ?? "edit the wording"}",
            _ => null
        }
    };

    /// <summary>Called by the page each time it appears: the shop, its orders and its dishes from the API.</summary>
    public async Task ReloadAsync()
    {
        OnPropertyChanged(nameof(Greeting));
        await LoadFromApiAsync();
    }

    [RelayCommand]
    private async Task Refresh() => await LoadFromApiAsync();

    private async Task LoadFromApiAsync()
    {
        _load?.Cancel();
        var load = _load = new CancellationTokenSource();
        IsLoading = true;
        try
        {
            var shop = await _seller.GetMyShopAsync(load.Token);
            if (load.IsCancellationRequested) return;
            if (shop.IsFailure)
            {
                Message = shop.Error!.Status == 404
                    ? "You don't have a shop yet. Open one from \"Become a Seller\"."
                    : shop.Error.DisplayMessage;
                return;
            }
            Shop = shop.Value;
            await LoadMyDishesAsync(load.Token);
            if (load.IsCancellationRequested) return;

            var stats = _seller.GetStatsAsync(load.Token);
            var waiting = _seller.GetOrdersAsync(OrderStatus.Paid, WaitingPager.Page, WaitingPager.PageSize, load.Token);
            var preparing = _seller.GetOrdersAsync(OrderStatus.Preparing, PreparingPager.Page, PreparingPager.PageSize,
                                                   load.Token);
            var onTheWay = _seller.GetOrdersAsync(OrderStatus.OutForDelivery, OutForDeliveryPager.Page,
                                                  OutForDeliveryPager.PageSize, load.Token);
            await Task.WhenAll(stats, waiting, preparing, onTheWay);
            if (load.IsCancellationRequested) return;

            var failed = new[] { (ApiResult)stats.Result, waiting.Result, preparing.Result, onTheWay.Result }
                .FirstOrDefault(r => r.IsFailure);
            Message = failed?.Error!.DisplayMessage ?? string.Empty;

            if (stats.Result.IsSuccess)
            {
                Stats = stats.Result.Value;
                ShowSales(Stats!.WeeklySales);
            }
            Fill(WaitingOrders, WaitingPager, waiting.Result);
            Fill(PreparingOrders, PreparingPager, preparing.Result);
            Fill(OutForDeliveryOrders, OutForDeliveryPager, onTheWay.Result);
            NotifyListsChanged();
        }
        catch (OperationCanceledException)
        {
            // A newer load replaced this one
        }
        catch (Exception)
        {
            Message = "We couldn't load your orders. Pull down or tap Refresh to try again.";
        }
        finally
        {
            if (_load == load) IsLoading = false;
        }
    }

    /// <summary>One list's new page (its pager's ◀ ▶ or "Show").</summary>
    private async Task LoadOrderPageAsync(OrderStatus status, ObservableCollection<ShopOrderCard> list, Paging pager)
    {
        var result = await _seller.GetOrdersAsync(status, pager.Page, pager.PageSize);
        if (result.IsFailure)
        {
            Message = result.Error!.DisplayMessage;
            return;
        }

        Fill(list, pager, result);
        NotifyListsChanged();
    }

    private static void Fill(ObservableCollection<ShopOrderCard> list, Paging pager,
                             ApiResult<PagedResult<ShopOrderSummaryDto>> result)
    {
        if (result.IsFailure) return;
        pager.Update(result.Value!.TotalCount);
        list.Clear();
        foreach (var order in result.Value.Items)
            list.Add(new ShopOrderCard { Order = order });
    }

    private void NotifyListsChanged()
    {
        OnPropertyChanged(nameof(WaitingTitle));
        OnPropertyChanged(nameof(HasNoWaitingOrders));
        OnPropertyChanged(nameof(PreparingTitle));
        OnPropertyChanged(nameof(HasPreparingOrders));
        OnPropertyChanged(nameof(OutForDeliveryTitle));
        OnPropertyChanged(nameof(HasOutForDeliveryOrders));
    }

    /// <summary>Mon–Sun bars from the server's daily order counts (the tallest day fills the chart).</summary>
    private void ShowSales(IReadOnlyList<DailySalesDto> days)
    {
        const double tallest = 64;
        var max = Math.Max(1, days.Count == 0 ? 0 : days.Max(d => d.Orders));

        WeeklySales.Clear();
        foreach (var day in days.OrderBy(d => d.Date))
        {
            var weekend = day.Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            WeeklySales.Add(new SalesBar(day.Date.ToString("ddd")[..1],
                                         Math.Max(8, tallest * day.Orders / max), weekend));
        }
    }

    /// <summary>Tap an order → its page: what the customer ordered, then Accept / Decline / Send out / Delivered.</summary>
    [RelayCommand]
    private async Task OpenOrder(ShopOrderCard card) =>
        await Shell.Current.GoToAsync("shoporder", new Dictionary<string, object> { ["OrderNumber"] = card.OrderNumber });

    /// <summary>"+ Add dish" → Add dish page.</summary>
    [RelayCommand]
    private async Task AddDish() => await Shell.Current.GoToAsync("dishedit");

    /// <summary>"Shop settings" → details, pictures, delivery fee, order cut-off and documents.</summary>
    [RelayCommand]
    private async Task ShopSettings() => await Shell.Current.GoToAsync("shopsettings");

    /// <summary>Tap a dish → Edit dish page.</summary>
    [RelayCommand]
    private async Task EditDish(Dish dish)
    {
        var mine = _allDishes.FirstOrDefault(d => d.Id.ToString() == dish.Id);
        if (mine is null) return;
        await Shell.Current.GoToAsync("dishedit", new Dictionary<string, object> { ["MyDish"] = mine });
    }

    [RelayCommand]
    private Task IncreasePrice(Dish dish) => NudgePriceAsync(dish, +1);

    [RelayCommand]
    private Task DecreasePrice(Dish dish) => NudgePriceAsync(dish, -1);

    /// <summary>− / +: one step (1, 5 or 100 depending on the price), kept inside the dish's allowed range.</summary>
    private async Task NudgePriceAsync(Dish dish, int direction)
    {
        var mine = _allDishes.FirstOrDefault(d => d.Id.ToString() == dish.Id);
        if (mine is null) return;

        var step = dish.Price >= 1000 ? 100m : dish.Price >= 100 ? 5m : 1m;
        var price = dish.Price + direction * step;
        if (mine.AllowedMinPrice is { } min && price < min) price = min;
        if (mine.AllowedMaxPrice is { } max && price > max) price = max;
        if (price <= 0 || price == dish.Price) return;

        var old = dish.Price;
        dish.Price = price;   // straight away; put back if the server says no
        var result = await _seller.ChangeDishPriceAsync(mine.Id, price);
        if (result.IsFailure)
        {
            dish.Price = old;
            Message = result.Error!.DisplayMessage;
        }
    }

    /// <summary>"View all →": opens Explore filtered to my shop's dishes, as buyers see them.</summary>
    [RelayCommand]
    private async Task ViewAllDishes()
    {
        if (Shop is null) return;
        await Shell.Current.GoToAsync(
            $"//listing?vendorId={Uri.EscapeDataString(Shop.Id.ToString())}&vendorName={Uri.EscapeDataString(Shop.Name)}");
    }
}
