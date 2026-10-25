using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Models;
using Phloem.Services;
using Phloem.Services.Api.Contracts.Commands;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;
using Phloem.Services.Api.Contracts.Queries;

namespace Phloem.ViewModels;

/// <summary>A route in the "From → To" picker.</summary>
public sealed class RouteChoice
{
    public required ShipmentRouteOptionDto Route { get; init; }
    public string Display => ShipmentText.Route(Route.FromCountry, Route.ToCountry);
}

/// <summary>A speed card (Standard / Express / Bulk) with its price and days.</summary>
public partial class SpeedChoice : ObservableObject
{
    public required ShipmentSpeedOptionDto Option { get; init; }
    public required Currency Currency { get; init; }
    public string Title => ShipmentText.Speed(Option.Speed);
    public string DaysText => ShipmentText.Days(Option.MinTransitDays, Option.MaxTransitDays);
    public string PriceText =>
        $"{ShipmentText.Money(Option.BaseCharge, Currency)} + {ShipmentText.Money(Option.PricePerKg, Currency)}/kg";

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>One line of "What we can't carry" (from the API).</summary>
public sealed class ParcelRuleRow
{
    public required ParcelItemRuleDto Rule { get; init; }
    public string Name => Rule.Name;
    public string? Detail => Rule.Detail;
    public bool HasDetail => !string.IsNullOrWhiteSpace(Rule.Detail);
}

/// <summary>An item category in the item's picker.</summary>
public sealed class CategoryChoice
{
    public required ShipmentItemCategory Value { get; init; }
    public string Name => ShipmentText.Category(Value);
}

/// <summary>One item the sender declares (a row in "What's in the parcel").</summary>
public partial class ParcelItem : ObservableObject
{
    [ObservableProperty]
    public partial CategoryChoice? Category { get; set; }

    [ObservableProperty]
    public partial string Description { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Quantity { get; set; } = "1";

    /// <summary>Value of ONE of these, in the sending country's currency (for customs).</summary>
    [ObservableProperty]
    public partial string UnitValue { get; set; } = string.Empty;
}

/// <summary>
/// Send Food to UK (route "international"): books a parcel on the API. It's its own booking, not part of the basket.
/// <list type="number">
///   <item><description>Pick the route (only routes an admin has switched on) and a speed.</description></item>
///   <item><description>Recipient, items, estimated weight → a live price estimate.</description></item>
///   <item><description>Book → a tracking number and where to drop the parcel off. It's weighed at the hub, then
///   the sender pays the final price from My shipments.</description></item>
/// </list>
/// Needs sign-in, and the server needs the sender's personal details (date of birth + home address).
/// </summary>
public partial class InternationalDeliveryViewModel : ObservableObject
{
    private readonly ShipmentService _shipments;
    private readonly SessionService _session;
    private readonly CartService _cart;
    private readonly ParcelRulesService _rules;
    private CancellationTokenSource? _quoteLoad;

    public InternationalDeliveryViewModel(ShipmentService shipments, SessionService session, CartService cart,
                                          ParcelRulesService rules)
    {
        _shipments = shipments;
        _session = session;
        _cart = cart;
        _rules = rules;
        Items.Add(NewItem());
    }

    // ---------- Route and speed ----------

    public ObservableCollection<RouteChoice> Routes { get; } = new();
    public ObservableCollection<SpeedChoice> Speeds { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DropOffText))]
    public partial RouteChoice? SelectedRoute { get; set; }

    [ObservableProperty]
    public partial SpeedChoice? SelectedSpeed { get; set; }

    public bool HasRoutes => Routes.Count > 0;
    public bool HasNoRoutes => !HasRoutes && !IsLoading;

    /// <summary>"Drop off at: 12 Ring Road, Accra · Mon–Sat 9am–5pm".</summary>
    public string DropOffText => SelectedRoute is { } r ? $"{r.Route.DropOffAddress} · {r.Route.DropOffHours}" : string.Empty;

    // ---------- Sender and recipient ----------

    [ObservableProperty]
    public partial string SenderPhone { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RecipientName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RecipientPhone { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RecipientAddressLine { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RecipientCity { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RecipientRegionOrPostcode { get; set; } = string.Empty;

    // ---------- Parcel ----------

    public IReadOnlyList<CategoryChoice> Categories { get; } =
        Enum.GetValues<ShipmentItemCategory>().Select(c => new CategoryChoice { Value = c }).ToList();

    public ObservableCollection<ParcelItem> Items { get; } = new();

    [ObservableProperty]
    public partial string EstimatedWeight { get; set; } = string.Empty;

    /// <summary>"About GH₵120.00 · 5–7 days", or a hint until it can be worked out.</summary>
    [ObservableProperty]
    public partial string QuoteText { get; set; } = "Choose a speed and enter the weight to see the price.";

    [ObservableProperty]
    public partial bool ConfirmedNoProhibitedItems { get; set; }

    // ---------- What we can't carry (from the API; an admin keeps it up to date) ----------

    public ObservableCollection<ParcelRuleRow> NotAllowedRules { get; } = new();
    public ObservableCollection<ParcelRuleRow> RestrictedRules { get; } = new();
    public bool HasNotAllowedRules => NotAllowedRules.Count > 0;
    public bool HasRestrictedRules => RestrictedRules.Count > 0;

    /// <summary>"We couldn't load the list…" when it can't be loaded and none is kept on the phone.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRulesMessage))]
    public partial string RulesMessage { get; set; } = string.Empty;

    public bool HasRulesMessage => RulesMessage.Length > 0;

    [ObservableProperty]
    public partial bool AcceptedTerms { get; set; }

    // ---------- State ----------

    public bool IsLoggedIn => _session.IsLoggedIn;
    public bool IsGuest => !IsLoggedIn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasNoRoutes), nameof(IsNotBusy))]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNotBusy))]
    public partial bool IsBooking { get; set; }

    public bool IsNotBusy => !IsLoading && !IsBooking;

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsError { get; set; }

    // ---------- After booking ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFormVisible))]
    public partial BookedShipmentDto? Booked { get; set; }

    public bool IsBooked => Booked is not null;
    public bool IsFormVisible => Booked is null;
    public string BookedPriceText => Booked is { } b ? $"About {ShipmentText.Money(b.EstimatedPrice, b.Currency)}" : string.Empty;

    partial void OnBookedChanged(BookedShipmentDto? value)
    {
        OnPropertyChanged(nameof(IsBooked));
        OnPropertyChanged(nameof(BookedPriceText));
    }

    // ═════════════════════════════ Loading ═════════════════════════════

    /// <summary>Called each time the page appears: the routes, and the sender's phone from their profile.</summary>
    public async Task LoadAsync()
    {
        OnPropertyChanged(nameof(IsLoggedIn));
        OnPropertyChanged(nameof(IsGuest));
        if (string.IsNullOrWhiteSpace(SenderPhone))
            SenderPhone = _session.Profile?.PhoneNumber ?? string.Empty;

        await LoadRulesAsync();
        if (Routes.Count > 0) return;

        IsLoading = true;
        try
        {
            var result = await _shipments.GetRoutesAsync();
            if (result.IsFailure)
            {
                ShowError(result.Error!.DisplayMessage);
                return;
            }

            foreach (var route in result.Value!) Routes.Add(new RouteChoice { Route = route });

            // Start with a route from the country picked on Home, if there is one
            SelectedRoute = Routes.FirstOrDefault(r => r.Route.FromCountry == _cart.SelectedCountry?.ApiCountry)
                            ?? Routes.FirstOrDefault();
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(HasRoutes));
            OnPropertyChanged(nameof(HasNoRoutes));
        }
    }

    /// <summary>Fills "What we can't carry" (each time the page appears, so a changed list shows).</summary>
    private async Task LoadRulesAsync()
    {
        var (rules, _) = await _rules.GetAsync();
        if (rules is null)
        {
            if (NotAllowedRules.Count == 0 && RestrictedRules.Count == 0)
                RulesMessage = "We couldn't load the list right now. Please check your internet connection.";
            return;
        }

        RulesMessage = string.Empty;
        NotAllowedRules.Clear();
        RestrictedRules.Clear();
        foreach (var rule in rules)
            (rule.Kind == ParcelItemRuleKind.Restricted ? RestrictedRules : NotAllowedRules).Add(new ParcelRuleRow { Rule = rule });
        OnPropertyChanged(nameof(HasNotAllowedRules));
        OnPropertyChanged(nameof(HasRestrictedRules));
    }

    partial void OnSelectedRouteChanged(RouteChoice? value)
    {
        Speeds.Clear();
        SelectedSpeed = null;
        if (value is null) return;

        foreach (var option in value.Route.Speeds)
            Speeds.Add(new SpeedChoice { Option = option, Currency = value.Route.Currency });
        if (Speeds.Count > 0) SelectSpeed(Speeds[0]);
    }

    [RelayCommand]
    private void SelectSpeed(SpeedChoice choice)
    {
        foreach (var s in Speeds) s.IsSelected = ReferenceEquals(s, choice);
        SelectedSpeed = choice;
    }

    partial void OnSelectedSpeedChanged(SpeedChoice? value) => RefreshQuote();
    partial void OnEstimatedWeightChanged(string value) => RefreshQuote();

    /// <summary>Asks the server for the estimate (waits a moment while the weight is typed).</summary>
    private async void RefreshQuote()
    {
        _quoteLoad?.Cancel();
        var load = _quoteLoad = new CancellationTokenSource();

        if (SelectedRoute is not { } route || SelectedSpeed is not { } speed || ParseDecimal(EstimatedWeight) is not { } kg
            || kg <= 0)
        {
            QuoteText = "Choose a speed and enter the weight to see the price.";
            return;
        }

        try
        {
            await Task.Delay(400, load.Token);
            var quote = await _shipments.QuoteAsync(
                new GetShipmentQuoteQuery(route.Route.FromCountry, route.Route.ToCountry, speed.Option.Speed, kg),
                load.Token);
            if (load.IsCancellationRequested) return;

            QuoteText = quote.IsSuccess
                ? $"About {ShipmentText.Money(quote.Value!.EstimatedPrice, quote.Value.Currency)} · " +
                  $"{ShipmentText.Days(quote.Value.MinTransitDays, quote.Value.MaxTransitDays)}"
                : quote.Error!.DisplayMessage;
        }
        catch (Exception)
        {
            // A newer estimate replaced this one, or the server couldn't be reached: the old text stays
        }
    }

    // ═════════════════════════════ Items ═════════════════════════════

    private ParcelItem NewItem() => new() { Category = Categories[0] };

    [RelayCommand]
    private void AddItem() => Items.Add(NewItem());

    [RelayCommand]
    private void RemoveItem(ParcelItem item)
    {
        if (Items.Count > 1) Items.Remove(item);
    }

    // ═════════════════════════════ Booking ═════════════════════════════

    [RelayCommand]
    private async Task Book()
    {
        if (!IsNotBusy) return;

        if (!_session.IsLoggedIn)
        {
            await GoToLogin();
            return;
        }

        var command = ReadBooking(out var problem);
        if (command is null)
        {
            ShowError(problem!);
            return;
        }

        IsBooking = true;
        try
        {
            var result = await _shipments.BookAsync(command);
            if (result.IsFailure)
            {
                // The server needs the sender's date of birth + home address first
                if (result.Error!.Code.Contains("PersonalDetails", StringComparison.OrdinalIgnoreCase))
                {
                    var go = await Shell.Current.DisplayAlertAsync("Your details are needed",
                        result.Error.DisplayMessage, "Add my details", "Not now");
                    if (go) await Shell.Current.GoToAsync("//account/editprofile");
                    return;
                }

                ShowError(result.Error.DisplayMessage);
                return;
            }

            StatusMessage = string.Empty;
            Booked = result.Value;
        }
        finally
        {
            IsBooking = false;
        }
    }

    /// <summary>The booking from the form; null with <paramref name="problem"/> when something's missing.</summary>
    private BookShipmentCommand? ReadBooking(out string? problem)
    {
        problem = null;
        if (SelectedRoute is not { } route) { problem = "Choose where the parcel is going."; return null; }
        if (SelectedSpeed is not { } speed) { problem = "Choose a delivery speed."; return null; }

        string[] required = [SenderPhone, RecipientName, RecipientPhone, RecipientAddressLine, RecipientCity,
                             RecipientRegionOrPostcode];
        if (required.Any(string.IsNullOrWhiteSpace))
        {
            problem = "Please fill in your phone and the recipient's name, phone and full address.";
            return null;
        }

        var items = new List<ShipmentItemInputDto>();
        foreach (var item in Items)
        {
            if (string.IsNullOrWhiteSpace(item.Description) || item.Category is null ||
                !int.TryParse(item.Quantity, out var qty) || qty < 1 ||
                ParseDecimal(item.UnitValue) is not { } value || value < 0)
            {
                problem = "For each item, enter what it is, how many, and the value of one.";
                return null;
            }

            items.Add(new ShipmentItemInputDto(item.Category.Value, item.Description.Trim(), qty, value));
        }

        if (ParseDecimal(EstimatedWeight) is not { } kg || kg <= 0)
        {
            problem = "Enter the parcel's estimated weight in kg, e.g. 4.5.";
            return null;
        }

        if (!ConfirmedNoProhibitedItems || !AcceptedTerms)
        {
            problem = "Please confirm your parcel has none of the items we can't carry, and accept the shipping terms.";
            return null;
        }

        return new BookShipmentCommand(route.Route.FromCountry, route.Route.ToCountry, SenderPhone.Trim(),
                                       RecipientName.Trim(), RecipientPhone.Trim(), RecipientAddressLine.Trim(),
                                       RecipientCity.Trim(), RecipientRegionOrPostcode.Trim(), items,
                                       speed.Option.Speed, kg, ConfirmedNoProhibitedItems, AcceptedTerms);
    }

    /// <summary>"Send another parcel": a fresh form (same route and phone).</summary>
    [RelayCommand]
    private void SendAnother()
    {
        Booked = null;
        RecipientName = RecipientPhone = RecipientAddressLine = RecipientCity = RecipientRegionOrPostcode = string.Empty;
        EstimatedWeight = string.Empty;
        ConfirmedNoProhibitedItems = AcceptedTerms = false;
        Items.Clear();
        Items.Add(NewItem());
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task GoToMyShipments() => await Shell.Current.GoToAsync("//account/myshipments");

    [RelayCommand]
    private async Task GoToLogin() => await Shell.Current.GoToAsync("//account/login");

    // ═════════════════════════════ Helpers ═════════════════════════════

    /// <summary>"4.5" or "4,5" → 4.5 (whatever the phone's language); null when it isn't a number.</summary>
    internal static decimal? ParseDecimal(string? text) =>
        decimal.TryParse((text ?? string.Empty).Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint,
                         CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private void ShowError(string message)
    {
        IsError = true;
        StatusMessage = message;
    }
}
