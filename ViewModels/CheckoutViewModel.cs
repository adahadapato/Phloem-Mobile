using Phloem.Models;
using Phloem.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.ViewModels;

/// <summary>
/// organizes and manages the checkout process, 
/// including cart items, payment methods, and order placement.
/// </summary>
public partial class CheckoutViewModel : ObservableObject
{
    private readonly CartService _cart;
    private readonly SessionService _session;
    private readonly OrderHistoryService _history;
    private readonly DeliveryAddressService _delivery;
    private readonly OrderService _orders;

    /// <summary>The server's prices and fees for the basket (null until checked).</summary>
    private BasketCheckDto? _check;
    private CancellationTokenSource? _checkLoad;
    private bool _placing;

    /// <summary>
    /// observable collection of cart items, reflecting 
    /// the current state of the user's shopping cart.
    /// </summary>
    public ObservableCollection<CartItem> Items => _cart.Items;

    /// <summary>
    /// observable collection of available payment methods, 
    /// allowing users to select their preferred option for completing the purchase.
    /// </summary>
    public ObservableCollection<PaymentMethodOption> PaymentMethods { get; } = new()
    {
        new PaymentMethodOption { Name = "Credit / debit card", Subtitle = "Ending 3281", Icon = "💳" },
        new PaymentMethodOption { Name = "Mobile money", Subtitle = "MTN, Vodafone, Airtel", Icon = "📱" },
        new PaymentMethodOption { Name = "Bank transfer", Subtitle = "Direct transfer", Icon = "🏦" },
    };

    /// <summary>
    /// property representing the 
    /// currently selected payment method,
    /// </summary>
    [ObservableProperty]
    public partial PaymentMethodOption SelectedPaymentMethod { get; set; }

    /// <summary>
    /// "Delivery time": As soon as possible, This evening, Tomorrow. Times a shop in the basket doesn't take right now
    /// are greyed out (after a shop's cut-off, e.g. 7pm, only Tomorrow).
    /// </summary>
    public IReadOnlyList<DeliveryTimeOption> DeliveryTimes { get; } = DeliveryTimeOption.All();

    /// <summary>The delivery time the buyer picked (sent when checking the basket and placing the order).</summary>
    public DeliveryWindow SelectedDeliveryWindow { get; private set; } = DeliveryWindow.Asap;

    /// <summary>e.g. "Orders for today closed at 7pm at Mama Ama's Kitchen. Choose Tomorrow." (empty when none).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDeliveryTimeNote))]
    public partial string DeliveryTimeNote { get; set; } = string.Empty;

    public bool HasDeliveryTimeNote => DeliveryTimeNote.Length > 0;

    /// <summary>
    /// property indicating whether the order has been placed,
    /// </summary>
    [ObservableProperty]
    public partial bool OrderPlaced { get; set; }

    /// <summary>
    /// property holding the confirmation message 
    /// displayed to the user after placing an order,
    /// </summary>
    [ObservableProperty]
    public partial string ConfirmationMessage { get; set; } = string.Empty;

    /// <summary>
    /// observable property storing the order 
    /// number generated upon order placement,
    /// </summary>
    [ObservableProperty]
    public partial string OrderNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets the currency symbol for the selected country.
    /// </summary>
    public string CurrencySymbol => ServerShops is { Count: > 0 } shops
        ? OrderService.SymbolOf(shops[0].Currency)
        : _cart.SelectedCountry?.CurrencySymbol ?? "£";

    /// <summary>The checked basket's shops, when the server has priced it (normal orders only).</summary>
    private IReadOnlyList<BasketShopDto>? ServerShops => _check?.Shops;

    /// <summary>
    /// Gets the subtotal amount for all items in the cart.
    /// </summary>
    public decimal Subtotal => ServerShops is { Count: > 0 } shops ? shops.Sum(s => s.Subtotal) : _cart.Subtotal;

    /// <summary>
    /// Gets the delivery fee for the current cart.
    /// </summary>
    public decimal DeliveryFee => ServerShops is { Count: > 0 } shops
        ? shops.Sum(s => s.DeliveryFee ?? 0m)
        : _cart.DeliveryFee;

    /// <summary>
    /// Gets the total amount for all items in the shopping cart.
    /// </summary>
    public decimal Total => Subtotal + DeliveryFee;

    /// <summary>
    /// Gets the total quantity of all items in the cart.
    /// </summary>
    public int ItemCount => _cart.Items.Sum(i => i.Quantity);

    /// <summary>
    /// Gets the country currently selected in the shopping cart.
    /// </summary>
    public CountryOption SelectedCountry => _cart.SelectedCountry;

    /// <summary>
    /// The line shown in the "Delivery address" card, e.g. "Home · 31 Camden High St, London": the address picked on
    /// the Delivery address page, or the signed-in user's default.
    /// </summary>
    public string DeliveryAddressText => _delivery.Describe(_session.IsLoggedIn ? _session.CurrentUser : null);

    /// <summary>
    /// creates a new instance of the CheckoutViewModel class, initializing it with 
    /// the provided cart service and setting up event handlers for cart item changes.
    /// </summary>
    public CheckoutViewModel(CartService cart, SessionService session,
                             OrderHistoryService history, DeliveryAddressService delivery, OrderService orders)
    {
        _cart = cart;
        _session = session;
        _history = history;
        _delivery = delivery;
        _orders = orders;
        SelectedPaymentMethod = PaymentMethods[0];
        ShowSelectedDeliveryTime();
        _cart.Items.CollectionChanged += (_, _) => CheckBasket();
    }

    /// <summary>
    /// refreshes the totals for subtotal, delivery fee, 
    /// total amount, and item count by notifying property changes.
    /// </summary>
    private void RefreshTotals()
    {
        OnPropertyChanged(nameof(Subtotal));
        OnPropertyChanged(nameof(DeliveryFee));
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(ItemCount));
        OnPropertyChanged(nameof(CurrencySymbol));
    }

    /// <summary>
    /// Asks the server for today's prices and delivery fees (works signed out). Until it answers — or if it can't —
    /// the basket's own prices are shown; placing the order always uses the server's.
    /// </summary>
    private async void CheckBasket()
    {
        RefreshTotals();
        _checkLoad?.Cancel();
        var load = _checkLoad = new CancellationTokenSource();

        try
        {
            var items = OrderService.ToBasketItems(_cart.Items);
            if (items.Count == 0)
            {
                _check = null;
                RefreshTotals();
                return;
            }

            var result = await _orders.CheckBasketAsync(items, SelectedDeliveryWindow, load.Token);
            if (load.IsCancellationRequested) return;

            _check = result.IsSuccess ? result.Value : null;
            RefreshTotals();

            // The picked time closed meanwhile (shop's cut-off)? Move to the first open one and check again
            if (UpdateDeliveryTimes() is { } moveTo)
            {
                SelectDeliveryWindow(moveTo);
                CheckBasket();
            }
        }
        catch (Exception)
        {
            // A newer check replaced this one, or the server couldn't be reached: the basket's prices stay
        }
    }

    /// <summary>
    /// NEW: called by the page each time it appears, so totals, the delivery address,
    /// the server's prices and the flag are always up to date.
    /// </summary>
    public void RefreshAll()
    {
        RefreshTotals();
        OnPropertyChanged(nameof(DeliveryAddressText));
        OnPropertyChanged(nameof(SelectedCountry));
        OnPropertyChanged(nameof(CurrencySymbol));
        CheckBasket();
    }

    /// <summary>
    /// command method to increase the quantity of a specified cart item,
    /// </summary>
    [RelayCommand]
    private void IncreaseQuantity(CartItem item)
    {
        item.Quantity++;
        CheckBasket();
    }

    /// <summary>
    /// command method to decrease the quantity of a specified cart item.
    /// </summary>
    [RelayCommand]
    private void DecreaseQuantity(CartItem item)
    {
        if (item.Quantity > 1)
            item.Quantity--;
        else
            _cart.RemoveItem(item);

        CheckBasket();
    }

    // ───────────────────────────── Delivery time ─────────────────────────────

    /// <summary>Tap a delivery time: re-prices the basket for it (some times cost extra).</summary>
    [RelayCommand]
    private async Task SelectDeliveryTime(DeliveryTimeOption option)
    {
        if (!option.IsOpen)
        {
            await Shell.Current.DisplayAlertAsync(option.Title,
                DeliveryTimeNote.Length > 0 ? DeliveryTimeNote : $"{option.Title} isn't available for this basket.", "OK");
            return;
        }

        if (option.Window == SelectedDeliveryWindow) return;
        SelectDeliveryWindow(option.Window);
        CheckBasket();
    }

    private void SelectDeliveryWindow(DeliveryWindow window)
    {
        SelectedDeliveryWindow = window;
        ShowSelectedDeliveryTime();
    }

    private void ShowSelectedDeliveryTime()
    {
        foreach (var option in DeliveryTimes)
            option.IsSelected = option.Window == SelectedDeliveryWindow;
    }

    /// <summary>
    /// Greys out the times a shop in the basket doesn't take right now, and explains why.
    /// </summary>
    /// <returns>The first open time when the picked one has closed; otherwise null.</returns>
    private DeliveryWindow? UpdateDeliveryTimes()
    {
        var shops = ServerShops ?? Array.Empty<BasketShopDto>();
        foreach (var option in DeliveryTimes)
            option.IsOpen = shops.All(s => s.OpenWindows.Contains(option.Window));

        // Shops past their cut-off: they only take Tomorrow
        var closed = shops.Where(s => !s.OpenWindows.Contains(DeliveryWindow.Asap)
                                      && s.OpenWindows.Contains(DeliveryWindow.Tomorrow)).ToList();
        DeliveryTimeNote = closed.Count switch
        {
            0 => string.Join("\n", shops.Select(s => s.DeliveryProblem).OfType<string>().Distinct()),
            1 => $"Orders for today closed at {CutoffText(closed[0].OrderCutoffTime)} at {closed[0].ShopName}. " +
                 "Choose Tomorrow.",
            _ => "Some shops in your basket have closed for today's orders. Choose Tomorrow."
        };

        var picked = DeliveryTimes.First(o => o.Window == SelectedDeliveryWindow);
        return !picked.IsOpen && DeliveryTimes.FirstOrDefault(o => o.IsOpen) is { } open ? open.Window : null;
    }

    /// <summary>19:00 → "7pm", 18:30 → "6:30pm".</summary>
    private static string CutoffText(TimeOnly time) =>
        time.ToString(time.Minute == 0 ? "%h" : "h:mm") + (time.Hour < 12 ? "am" : "pm");

    /// <summary>
    /// "Change delivery details →": the Delivery address page (pick a saved address or add one).
    /// </summary>
    [RelayCommand]
    private async Task ChangeDeliveryDetails()
        => await Shell.Current.GoToAsync("address");

    /// <summary>
    /// "Place order". Normal orders go to the API:
    /// <list type="number">
    ///   <item><description>signed in? (otherwise → Log in)</description></item>
    ///   <item><description>a saved delivery address? (otherwise → Delivery address)</description></item>
    ///   <item><description>place: the API makes one order per shop and gives the order numbers</description></item>
    ///   <item><description>pay each one with a TEST payment (no money moves) until real payments are connected</description></item>
    /// </list>
    /// Parcels ("Send Food to UK") are booked separately, on their own page.
    /// </summary>
    [RelayCommand]
    private async Task PlaceOrder()
    {
        if (_placing || SelectedPaymentMethod is null || _cart.Items.Count == 0) return;
        _placing = true;
        try
        {
            await PlaceOrderOnApiAsync();
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Couldn't place order", ex.Message, "OK");
        }
        finally
        {
            _placing = false;
        }
    }

    private async Task PlaceOrderOnApiAsync()
    {
        // 1. Signed in
        if (!_session.IsLoggedIn || _session.CurrentUser is null)
        {
            await Shell.Current.DisplayAlertAsync("Log in to order",
                "Please log in (or sign up) to place your order. Your basket will still be here.", "OK");
            await Shell.Current.GoToAsync("//account/login");
            return;
        }

        // 2. A saved delivery address
        var address = _delivery.Resolve(_session.CurrentUser);
        if (address is null || !Guid.TryParse(address.Id, out var addressId))
        {
            await Shell.Current.DisplayAlertAsync("Delivery address",
                "Please add the address we should deliver to.", "OK");
            await Shell.Current.GoToAsync("address");
            return;
        }

        var items = OrderService.ToBasketItems(_cart.Items);
        if (items.Count == 0)
        {
            await Shell.Current.DisplayAlertAsync("Couldn't place order",
                "These dishes can't be ordered any more. Please add them again from Browse & Order.", "OK");
            return;
        }

        // 3. Place: one order per shop
        var placed = await _orders.PlaceAsync(items, addressId, SelectedDeliveryWindow);
        if (placed.IsFailure && placed.Error!.Code == AppNav.TermsNotAcceptedCode)
        {
            // New Terms: accept them (then tap Place order again)
            await AppNav.OpenTermsUpdateAsync();
            return;
        }
        if (placed.IsFailure)
        {
            await Shell.Current.DisplayAlertAsync("Couldn't place order", placed.Error!.DisplayMessage, "OK");
            CheckBasket();   // show today's prices / what's missing
            return;
        }

        // 4. Test payment for each order
        var unpaid = new List<string>();
        foreach (var order in placed.Value!)
        {
            var paid = await _orders.PayWithTestPaymentAsync(order.OrderNumber);
            if (paid.IsFailure) unpaid.Add($"{order.OrderNumber}: {paid.Error!.DisplayMessage}");
        }

        var numbers = placed.Value.Select(o => o.OrderNumber).ToList();
        OrderNumber = string.Join(", ", numbers);
        var perShop = numbers.Count > 1 ? $"Your basket became {numbers.Count} orders, one for each shop. " : string.Empty;
        ConfirmationMessage = unpaid.Count == 0
            ? $"{perShop}It's paid with a TEST payment (no money was taken). " +
              $"You can cancel it from Orders within {PlacedOrder.CancelWindow.TotalMinutes:0} minutes."
            : $"{perShop}It's saved in Orders, but the test payment didn't go through:\n{string.Join("\n", unpaid)}";

        _cart.Clear();
        _check = null;
        OrderPlaced = true;
        RefreshAll();
        await _history.LoadAsync();
    }

    /// <summary>
    /// command method to navigate back to the shopping listing page after an order has been placed.
    /// </summary>
    [RelayCommand]
    private async Task BackToShopping()
    {
        OrderPlaced = false;
        await Shell.Current.GoToAsync("//listing");
    }
}
