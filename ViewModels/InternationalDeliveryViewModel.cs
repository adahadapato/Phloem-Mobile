using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Helpers;
using Phloem.Models;
using Phloem.Views;
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

/// <summary>One line of the price breakdown ("Fragile handling  £10.00").</summary>
public sealed record PriceLine(string Label, string Amount, string? Note = null)
{
    public bool HasNote => !string.IsNullOrEmpty(Note);
}

/// <summary>
/// Send a parcel (route "international"): books a parcel on the API. It's its own booking, not part of the basket.
/// <list type="number">
///   <item><description>Route (only routes an admin has switched on) and speed.</description></item>
///   <item><description>Boxes: weight of one box, its outside size (explained with a drawing) and how many; each says
///   at once what it counts as. Fragile.</description></item>
///   <item><description>Items for customs.</description></item>
///   <item><description>Getting it to us: drop off at the hub, or home pickup. How it reaches them: to the door
///   (charge by the state's zone), or they collect from our hub. Options a route doesn't offer are greyed out.</description></item>
///   <item><description>Recipient: where the destination has lists (Nigeria), type the town and pick a suggestion
///   (fills in State + LGA), or choose State and LGA from full-screen search lists; a town we don't know gets an LGA
///   suggestion (our list, else the AI) to check.</description></item>
///   <item><description>A live price, part by part; Book → tracking number and what happens next.</description></item>
/// </list>
/// Needs sign-in, and the server needs the sender's personal details (date of birth + home address).
/// </summary>
public partial class InternationalDeliveryViewModel : ObservableObject
{
    private readonly ShipmentService _shipments;
    private readonly SessionService _session;
    private readonly CartService _cart;
    private readonly ParcelRulesService _rules;
    private readonly LocationService _locations;
    private CancellationTokenSource? _quoteLoad;
    private CancellationTokenSource? _townLoad;
    private bool _settingTown;   // true while the town is filled in by code (no new suggestions)

    public InternationalDeliveryViewModel(ShipmentService shipments, SessionService session, CartService cart,
                                          ParcelRulesService rules, LocationService locations)
    {
        _shipments = shipments;
        _session = session;
        _cart = cart;
        _rules = rules;
        _locations = locations;
        Items.Add(NewItem());
        AddBoxLine();
    }

    // ---------- Route and speed ----------

    public ObservableCollection<RouteChoice> Routes { get; } = new();
    public ObservableCollection<SpeedChoice> Speeds { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DropOffText), nameof(PickupOffered), nameof(PickupPriceText),
                              nameof(CollectionOffered), nameof(CollectionText), nameof(FragileText),
                              nameof(HasLocationList), nameof(HasNoLocationList), nameof(PostcodePlaceholder),
                              nameof(PickupPostcodePlaceholder), nameof(Divisor), nameof(SizeExplainer),
                              nameof(RecipientPhonePlaceholder))]
    public partial RouteChoice? SelectedRoute { get; set; }

    [ObservableProperty]
    public partial SpeedChoice? SelectedSpeed { get; set; }

    public bool HasRoutes => Routes.Count > 0;
    public bool HasNoRoutes => !HasRoutes && !IsLoading;

    /// <summary>"12 Ring Road, Accra · Mon–Sat 9am–5pm".</summary>
    public string DropOffText => SelectedRoute is { } r ? $"{r.Route.DropOffAddress} · {r.Route.DropOffHours}" : string.Empty;

    private ShipmentRouteOptionDto? Route => SelectedRoute?.Route;
    private Currency RouteCurrency => Route?.Currency ?? Currency.GBP;

    // ---------- Boxes ----------

    public ObservableCollection<ParcelBoxDraft> Boxes { get; } = new();

    /// <summary>The route's divisor (usually 5000).</summary>
    public int Divisor => Route?.VolumeDivisor ?? 5000;

    /// <summary>The "Why we ask for the size" text.</summary>
    public string SizeExplainer =>
        $"Planes charge for the space a parcel takes as well as its weight. So for each box we work out a size weight: " +
        $"length × width × height in cm, divided by {Divisor:N0}. You pay for whichever is higher, the real weight or the " +
        $"size weight, rounded up to the next 0.5 kg.";

    [ObservableProperty]
    public partial bool IsExplainerOpen { get; set; }

    [ObservableProperty]
    public partial bool IsFragile { get; set; }

    /// <summary>"+£10.00" or "No extra charge".</summary>
    public string FragileText => Route is { FragileCharge: > 0 } r
        ? $"+{ShipmentText.Money(r.FragileCharge, r.Currency)}"
        : "No extra charge";

    // ---------- Getting it to us / how it reaches them ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDropOff), nameof(IsPickup), nameof(HandOverSummary))]
    public partial ParcelHandOver HandOver { get; set; } = ParcelHandOver.HubDropOff;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsToDoor), nameof(IsCollect), nameof(DeliverySummary), nameof(NeedsStreet))]
    public partial ParcelDelivery Delivery { get; set; } = ParcelDelivery.ToDoor;

    public bool PickupOffered => Route?.HomePickupCharge is not null;
    public bool CollectionOffered => !string.IsNullOrWhiteSpace(Route?.CollectionHubAddress);

    /// <summary>A choice the route doesn't offer falls back to the free one.</summary>
    private ParcelHandOver HandOverChoice => HandOver == ParcelHandOver.HomePickup && PickupOffered ? ParcelHandOver.HomePickup : ParcelHandOver.HubDropOff;
    private ParcelDelivery DeliveryChoice => Delivery == ParcelDelivery.CollectFromHub && CollectionOffered ? ParcelDelivery.CollectFromHub : ParcelDelivery.ToDoor;

    public bool IsDropOff => HandOverChoice == ParcelHandOver.HubDropOff;
    public bool IsPickup => HandOverChoice == ParcelHandOver.HomePickup;
    public bool IsToDoor => DeliveryChoice == ParcelDelivery.ToDoor;
    public bool IsCollect => DeliveryChoice == ParcelDelivery.CollectFromHub;
    public bool NeedsStreet => IsToDoor;

    public string PickupPriceText => Route?.HomePickupCharge is { } c ? $"+{ShipmentText.Money(c, RouteCurrency)} · we call you to arrange a time"
                                                                        : "Not available on this route yet";
    public string CollectionText => CollectionOffered ? $"No delivery charge\n{Route!.CollectionHubAddress}\n{Route.CollectionHubHours}"
                                                      : "Not available on this route yet";

    [ObservableProperty]
    public partial string DoorDeliveryText { get; set; } = "A courier brings it to the address";

    public string HandOverSummary => IsPickup ? "Collected from you" : "You drop it off";
    public string DeliverySummary => IsCollect ? "Collect from our hub" : "Delivered to the door";

    [ObservableProperty]
    public partial string PickupAddressLine { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PickupCity { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PickupPostcode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PickupNotes { get; set; } = string.Empty;

    public string PickupPostcodePlaceholder => Route?.FromCountry == Country.UnitedKingdom ? "Postcode" : "Region";

    // ---------- Sender and recipient ----------

    [ObservableProperty]
    public partial string SenderPhone { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RecipientName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string RecipientPhone { get; set; } = string.Empty;

    public string RecipientPhonePlaceholder => Route?.ToCountry switch
    {
        Country.Nigeria => "Phone, e.g. +234 803 123 4567",
        Country.Ghana => "Phone, e.g. +233 24 555 0192",
        _ => "Phone, e.g. +44 7700 900123"
    };

    [ObservableProperty]
    public partial string RecipientAddressLine { get; set; } = string.Empty;

    /// <summary>Town or area (Nigeria), or "City or town" elsewhere.</summary>
    [ObservableProperty]
    public partial string RecipientCity { get; set; } = string.Empty;

    /// <summary>Postcode / region text where the destination has no lists.</summary>
    [ObservableProperty]
    public partial string RecipientRegionOrPostcode { get; set; } = string.Empty;

    public string PostcodePlaceholder => Route?.ToCountry == Country.UnitedKingdom ? "Postcode" : "Region";

    // ---------- Recipient's State / LGA / town (destinations with lists) ----------

    public bool HasLocationList => Route?.HasLocationList == true;
    public bool HasNoLocationList => !HasLocationList;

    [ObservableProperty]
    public partial string RegionLabel { get; set; } = "State";

    [ObservableProperty]
    public partial string DistrictLabel { get; set; } = "LGA";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegionText), nameof(DistrictText), nameof(CanChooseDistrict))]
    public partial RegionDto? SelectedRegion { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DistrictText))]
    public partial DistrictDto? SelectedDistrict { get; set; }

    private Guid? _townId;

    public string RegionText => SelectedRegion?.Name ?? $"Choose the {RegionLabel.ToLowerInvariant()}";
    public string DistrictText => SelectedDistrict?.Name ??
        (SelectedRegion is null ? $"Choose the {RegionLabel.ToLowerInvariant()} first" : $"Choose the {DistrictLabel}");
    public bool CanChooseDistrict => SelectedRegion is not null;

    public ObservableCollection<TownDto> TownSuggestions { get; } = new();

    [ObservableProperty]
    public partial bool ShowTownSuggestions { get; set; }

    /// <summary>"Lekki Phase 1: Eti-Osa LGA, Lagos." or the AI's suggestion to check.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLocationHint))]
    public partial string LocationHint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool LocationHintIsAi { get; set; }

    public bool HasLocationHint => LocationHint.Length > 0;

    // ---------- Items ----------

    public IReadOnlyList<CategoryChoice> Categories { get; } =
        Enum.GetValues<ShipmentItemCategory>().Select(c => new CategoryChoice { Value = c }).ToList();

    public ObservableCollection<ParcelItem> Items { get; } = new();

    // ---------- Price ----------

    /// <summary>"About £157.00 · 5–7 days", or a hint until it can be worked out.</summary>
    [ObservableProperty]
    public partial string QuoteText { get; set; } = "Enter your boxes to see the price.";

    /// <summary>"2 boxes · 16 kg → charged as 39 kg".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWeightSummary))]
    public partial string WeightSummary { get; set; } = string.Empty;

    public bool HasWeightSummary => WeightSummary.Length > 0;

    /// <summary>Shipping, fragile, pickup, door delivery.</summary>
    public ObservableCollection<PriceLine> PriceLines { get; } = new();

    [ObservableProperty]
    public partial bool HasPriceLines { get; set; }

    // ---------- Confirmations and rules ----------

    [ObservableProperty]
    public partial bool ConfirmedNoProhibitedItems { get; set; }

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
    [NotifyPropertyChangedFor(nameof(IsFormVisible), nameof(IsBooked), nameof(BookedPriceText), nameof(BookedIsPickup),
                              nameof(BookedIsDropOff), nameof(BookedCollectionText), nameof(BookedHasCollection))]
    public partial BookedShipmentDto? Booked { get; set; }

    public bool IsBooked => Booked is not null;
    public bool IsFormVisible => Booked is null;
    public string BookedPriceText => Booked is { } b ? $"About {ShipmentText.Money(b.EstimatedPrice, b.Currency)}" : string.Empty;
    public bool BookedIsPickup => Booked?.HandOver == ParcelHandOver.HomePickup;
    public bool BookedIsDropOff => Booked is not null && !BookedIsPickup;
    public bool BookedHasCollection => Booked is { Delivery: ParcelDelivery.CollectFromHub, CollectionHubAddress: not null };
    public string BookedCollectionText => Booked is { } b && BookedHasCollection
        ? $"The recipient collects it from {b.CollectionHubAddress} ({b.CollectionHubHours}). We'll tell them when it's ready."
        : string.Empty;

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
        foreach (var box in Boxes) box.Divisor = Divisor;
        ClearPlace();
        RefreshChoices();
        if (value is null) return;

        foreach (var option in value.Route.Speeds)
            Speeds.Add(new SpeedChoice { Option = option, Currency = value.Route.Currency });
        if (Speeds.Count > 0) SelectSpeed(Speeds[0]);
        _ = LoadLabelsAsync(value.Route);
    }

    /// <summary>"State" / "LGA" (Nigeria) or "Region" / "District" (Ghana) for the route's destination.</summary>
    private async Task LoadLabelsAsync(ShipmentRouteOptionDto route)
    {
        if (!route.HasLocationList) return;
        var result = await _locations.GetRegionsAsync(route.ToCountry);
        if (result.IsFailure || Route != route) return;
        RegionLabel = result.Value!.RegionLabel;
        DistrictLabel = result.Value.DistrictLabel;
        OnPropertyChanged(nameof(RegionText));
        OnPropertyChanged(nameof(DistrictText));
    }

    [RelayCommand]
    private void SelectSpeed(SpeedChoice choice)
    {
        foreach (var s in Speeds) s.IsSelected = ReferenceEquals(s, choice);
        SelectedSpeed = choice;
    }

    partial void OnSelectedSpeedChanged(SpeedChoice? value) => RefreshQuote();
    partial void OnIsFragileChanged(bool value) => RefreshQuote();
    partial void OnHandOverChanged(ParcelHandOver value) => RefreshQuote();
    partial void OnDeliveryChanged(ParcelDelivery value) => RefreshQuote();
    partial void OnSelectedRegionChanged(RegionDto? value) => RefreshQuote();

    /// <summary>The route changed: what it offers may have changed too.</summary>
    private void RefreshChoices()
    {
        OnPropertyChanged(nameof(IsDropOff));
        OnPropertyChanged(nameof(IsPickup));
        OnPropertyChanged(nameof(IsToDoor));
        OnPropertyChanged(nameof(IsCollect));
        OnPropertyChanged(nameof(NeedsStreet));
        OnPropertyChanged(nameof(HandOverSummary));
        OnPropertyChanged(nameof(DeliverySummary));
    }

    // ═════════════════════════════ Boxes ═════════════════════════════

    private void AddBoxLine()
    {
        var box = new ParcelBoxDraft { Divisor = Divisor };
        box.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ParcelBoxDraft.CountsText)) RefreshQuote();
        };
        Boxes.Add(box);
        RenumberBoxes();
    }

    private void RenumberBoxes()
    {
        for (var i = 0; i < Boxes.Count; i++)
        {
            Boxes[i].Title = Boxes.Count == 1 ? "Your box" : $"Box {i + 1}";
            Boxes[i].CanRemove = Boxes.Count > 1;
        }
    }

    [RelayCommand]
    private void AddBox()
    {
        if (Boxes.Count >= ParcelMath.MaxLines) return;
        AddBoxLine();
    }

    [RelayCommand]
    private void RemoveBox(ParcelBoxDraft box)
    {
        if (Boxes.Count <= 1) return;
        Boxes.Remove(box);
        RenumberBoxes();
        RefreshQuote();
    }

    [RelayCommand]
    private void ToggleExplainer() => IsExplainerOpen = !IsExplainerOpen;

    private bool BoxesOk => Boxes.All(b => b.IsValid) && Boxes.Sum(b => b.TotalWeightKg) <= ParcelMath.MaxParcelKg;

    // ═════════════════════════════ Choices ═════════════════════════════

    [RelayCommand]
    private void ChooseHandOver(string choice)
    {
        if (choice == nameof(ParcelHandOver.HomePickup) && !PickupOffered) return;
        HandOver = Enum.Parse<ParcelHandOver>(choice);
    }

    [RelayCommand]
    private void ChooseDelivery(string choice)
    {
        if (choice == nameof(ParcelDelivery.CollectFromHub) && !CollectionOffered) return;
        Delivery = Enum.Parse<ParcelDelivery>(choice);
    }

    // ═════════════════════════════ State / LGA / town ═════════════════════════════

    /// <summary>Opens the full-screen State list (type to search, small typos forgiven).</summary>
    [RelayCommand]
    private async Task ChooseRegion()
    {
        if (Route is not { } route) return;
        var regions = await _locations.GetRegionsAsync(route.ToCountry);
        if (regions.IsFailure) { ShowError(regions.Error!.DisplayMessage); return; }

        var picked = await LocationPickerPage.PickAsync($"Choose the {RegionLabel.ToLowerInvariant()}",
            regions.Value!.Regions.Select(r => new LocationOption(r.Id, r.Name, null)).ToList(), SelectedRegion?.Id);
        if (picked is null) return;

        SelectedRegion = regions.Value.Regions.First(r => r.Id == picked.Id);
        SelectedDistrict = null;
        _townId = null;
        LocationHint = string.Empty;
        await FindDistrictAsync();
    }

    /// <summary>Opens the full-screen LGA list for the chosen state.</summary>
    [RelayCommand]
    private async Task ChooseDistrict()
    {
        if (SelectedRegion is not { } region) return;
        var districts = await _locations.GetDistrictsAsync(region.Id);
        if (districts.IsFailure) { ShowError(districts.Error!.DisplayMessage); return; }

        var picked = await LocationPickerPage.PickAsync($"Choose the {DistrictLabel}",
            districts.Value!.Select(d => new LocationOption(d.Id, d.Name, null)).ToList(), SelectedDistrict?.Id);
        if (picked is null) return;

        SelectedDistrict = districts.Value!.First(d => d.Id == picked.Id);
        _townId = null;
        LocationHint = string.Empty;
    }

    /// <summary>Suggestions as the town is typed (waits a moment between key presses).</summary>
    partial void OnRecipientCityChanged(string value)
    {
        if (_settingTown || !HasLocationList || Route is not { } route) return;
        _townId = null;
        LocationHint = string.Empty;
        _townLoad?.Cancel();
        var load = _townLoad = new CancellationTokenSource();

        if (value.Trim().Length < 2)
        {
            TownSuggestions.Clear();
            ShowTownSuggestions = false;
            return;
        }
        _ = SearchTownsAsync(route.ToCountry, value.Trim(), load);
    }

    private async Task SearchTownsAsync(Country country, string text, CancellationTokenSource load)
    {
        try
        {
            await Task.Delay(300, load.Token);
            var result = await _locations.SearchTownsAsync(country, text, SelectedRegion?.Id, load.Token);
            if (load.IsCancellationRequested || result.IsFailure) return;

            TownSuggestions.Clear();
            foreach (var town in result.Value!) TownSuggestions.Add(town);
            ShowTownSuggestions = TownSuggestions.Count > 0;
        }
        catch (Exception)
        {
            // A newer search replaced this one, or the server couldn't be reached
        }
    }

    /// <summary>A suggestion was tapped: fills in the town, State and LGA.</summary>
    [RelayCommand]
    private void PickTown(TownDto town)
    {
        _townLoad?.Cancel();
        _settingTown = true;
        RecipientCity = town.Name;
        _settingTown = false;
        _townId = town.TownId;
        SelectedRegion = new RegionDto(town.RegionId, town.RegionName, string.Empty);
        SelectedDistrict = new DistrictDto(town.DistrictId, town.DistrictName);
        ShowTownSuggestions = false;
        TownSuggestions.Clear();
        LocationHintIsAi = false;
        LocationHint = $"{town.Name}: {town.DistrictName} {DistrictLabel}, {town.RegionName}.";
    }

    /// <summary>The town box lost focus: hide the suggestions and, if needed, find the LGA.</summary>
    public async Task TownLeftAsync()
    {
        await Task.Delay(250);   // let a tap on a suggestion land first
        ShowTownSuggestions = false;
        await FindDistrictAsync();
    }

    /// <summary>
    /// A typed town we don't know, with a state but no LGA: ask which LGA it's in (our list first, then the AI) and
    /// pre-fill it for the customer to check.
    /// </summary>
    private async Task FindDistrictAsync()
    {
        if (SelectedRegion is not { } region || SelectedDistrict is not null || _townId is not null) return;
        var town = RecipientCity.Trim();
        if (town.Length < 2) return;

        LocationHintIsAi = false;
        LocationHint = $"Looking up which {DistrictLabel} {town} is in…";
        var result = await _locations.SuggestDistrictAsync(region.Id, town);
        if (SelectedRegion?.Id != region.Id || SelectedDistrict is not null) return;   // changed meanwhile

        if (result.IsFailure || result.Value!.DistrictId is not { } districtId)
        {
            LocationHint = $"We don't know {town} yet. Please choose its {DistrictLabel}.";
            return;
        }

        var s = result.Value;
        _settingTown = true;
        RecipientCity = s.TownName;
        _settingTown = false;
        _townId = s.TownId;
        SelectedDistrict = new DistrictDto(districtId, s.DistrictName!);
        LocationHintIsAi = s.FromAi;
        LocationHint = s.FromAi
            ? $"Suggested: {s.TownName} is {(s.Confidence == "high" ? "" : "probably ")}in {s.DistrictName} {DistrictLabel}. Please check, and change it if it's wrong."
            : $"{s.TownName} is in {s.DistrictName} {DistrictLabel}.";
    }

    private void ClearPlace()
    {
        SelectedRegion = null;
        SelectedDistrict = null;
        _townId = null;
        LocationHint = string.Empty;
        TownSuggestions.Clear();
        ShowTownSuggestions = false;
    }

    // ═════════════════════════════ Price ═════════════════════════════

    /// <summary>Asks the server for the estimate (waits a moment while things are typed).</summary>
    private async void RefreshQuote()
    {
        _quoteLoad?.Cancel();
        var load = _quoteLoad = new CancellationTokenSource();

        if (SelectedRoute is not { } route || SelectedSpeed is not { } speed || Boxes.Count == 0 || !BoxesOk)
        {
            QuoteText = Boxes.Sum(b => b.TotalWeightKg) > ParcelMath.MaxParcelKg
                ? $"A parcel can weigh at most {ParcelMath.MaxParcelKg:0} kg. Please send it as two parcels."
                : "Enter your boxes to see the price.";
            WeightSummary = string.Empty;
            PriceLines.Clear();
            HasPriceLines = false;
            return;
        }

        try
        {
            await Task.Delay(400, load.Token);
            var quote = await _shipments.QuoteAsync(
                new GetShipmentQuoteQuery(route.Route.FromCountry, route.Route.ToCountry, speed.Option.Speed,
                                          Boxes.Select(b => b.ToInput()).ToList(), IsFragile, HandOverChoice,
                                          DeliveryChoice, HasLocationList ? SelectedRegion?.Id : null),
                load.Token);
            if (load.IsCancellationRequested) return;

            if (quote.IsFailure)
            {
                QuoteText = quote.Error!.DisplayMessage;
                return;
            }

            ShowQuote(quote.Value!);
        }
        catch (Exception)
        {
            // A newer estimate replaced this one, or the server couldn't be reached: the old text stays
        }
    }

    private void ShowQuote(ShipmentQuoteDto q)
    {
        QuoteText = $"About {ShipmentText.Money(q.EstimatedPrice, q.Currency)} · {ShipmentText.Days(q.MinTransitDays, q.MaxTransitDays)}";

        var count = q.Boxes?.Sum(b => b.Quantity) ?? 1;
        WeightSummary = $"{count} box{(count == 1 ? "" : "es")} · {ParcelMath.Kg(q.EstimatedWeightKg)} → charged as {ParcelMath.Kg(q.ChargeableWeightKg)}";

        PriceLines.Clear();
        PriceLines.Add(new PriceLine("Shipping", ShipmentText.Money(q.FreightCharge, q.Currency),
            $"{ShipmentText.Money(q.BaseCharge, q.Currency)} + {ShipmentText.Money(q.PricePerKg, q.Currency)} × {ParcelMath.Kg(q.ChargeableWeightKg)}"));
        if (q.FragileCharge > 0) PriceLines.Add(new PriceLine("Fragile handling", ShipmentText.Money(q.FragileCharge, q.Currency)));
        if (q.HomePickupCharge > 0) PriceLines.Add(new PriceLine("Home pickup", ShipmentText.Money(q.HomePickupCharge, q.Currency)));
        if (q.DoorDeliveryCharge > 0)
            PriceLines.Add(new PriceLine(q.DeliveryZoneName is null ? "Door delivery" : $"Door delivery ({q.DeliveryZoneName})",
                                         ShipmentText.Money(q.DoorDeliveryCharge, q.Currency)));
        HasPriceLines = true;

        DoorDeliveryText = q.DoorDeliveryCharge > 0
            ? $"+{ShipmentText.Money(q.DoorDeliveryCharge, q.Currency)}{(q.DeliveryZoneName is null ? "" : $" ({q.DeliveryZoneName})")}"
            : HasLocationList && SelectedRegion is null ? "Charge depends on the state" : "A courier brings it to the address";
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

        if (!BoxesOk)
        {
            problem = Boxes.Sum(b => b.TotalWeightKg) > ParcelMath.MaxParcelKg
                ? $"A parcel can weigh at most {ParcelMath.MaxParcelKg:0} kg in total. Please send it as two parcels."
                : "Please check your boxes: each needs a weight, and either all three sides or none.";
            return null;
        }

        string[] required = [SenderPhone, RecipientName, RecipientPhone, RecipientCity];
        if (required.Any(string.IsNullOrWhiteSpace) || (IsToDoor && string.IsNullOrWhiteSpace(RecipientAddressLine)) ||
            (!HasLocationList && string.IsNullOrWhiteSpace(RecipientRegionOrPostcode)))
        {
            problem = "Please fill in your phone and the recipient's name, phone and address.";
            return null;
        }

        if (HasLocationList && (SelectedRegion is null || SelectedDistrict is null))
        {
            problem = $"Please choose the recipient's {RegionLabel.ToLowerInvariant()} and {DistrictLabel}.";
            return null;
        }

        if (IsPickup && new[] { PickupAddressLine, PickupCity, PickupPostcode }.Any(string.IsNullOrWhiteSpace))
        {
            problem = "Please give the address where we should collect your parcel.";
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

        if (!ConfirmedNoProhibitedItems || !AcceptedTerms)
        {
            problem = "Please confirm your parcel has none of the items we can't carry, and accept the shipping terms.";
            return null;
        }

        var pickup = IsPickup
            ? new ShipmentPickupInputDto(PickupAddressLine.Trim(), PickupCity.Trim(), PickupPostcode.Trim(),
                                         string.IsNullOrWhiteSpace(PickupNotes) ? null : PickupNotes.Trim())
            : null;

        return new BookShipmentCommand(
            route.Route.FromCountry, route.Route.ToCountry, SenderPhone.Trim(),
            RecipientName.Trim(), RecipientPhone.Trim(), IsToDoor ? RecipientAddressLine.Trim() : string.Empty,
            RecipientCity.Trim(), HasLocationList ? SelectedRegion!.Name : RecipientRegionOrPostcode.Trim(), items,
            speed.Option.Speed, null, ConfirmedNoProhibitedItems, AcceptedTerms,
            Boxes.Select(b => b.ToInput()).ToList(), IsFragile, HandOverChoice, pickup, DeliveryChoice,
            HasLocationList ? SelectedRegion!.Id : null, HasLocationList ? SelectedDistrict!.Id : null,
            HasLocationList ? _townId : null);
    }

    /// <summary>"Send another parcel": a fresh form (same route, phone and pickup address).</summary>
    [RelayCommand]
    private void SendAnother()
    {
        Booked = null;
        RecipientName = RecipientPhone = RecipientAddressLine = RecipientRegionOrPostcode = string.Empty;
        _settingTown = true;
        RecipientCity = string.Empty;
        _settingTown = false;
        ClearPlace();
        IsFragile = false;
        ConfirmedNoProhibitedItems = AcceptedTerms = false;
        Items.Clear();
        Items.Add(NewItem());
        Boxes.Clear();
        AddBoxLine();
        StatusMessage = string.Empty;
    }

    [RelayCommand]
    private async Task GoToMyShipments() => await Shell.Current.GoToAsync("//account/myshipments");

    [RelayCommand]
    private async Task GoToLogin() => await Shell.Current.GoToAsync("//account/login");

    // ═════════════════════════════ Helpers ═════════════════════════════

    /// <summary>"4.5" or "4,5" → 4.5 (whatever the phone's language); null when it isn't a number.</summary>
    internal static decimal? ParseDecimal(string? text) => ParcelMath.Parse(text);

    private void ShowError(string message)
    {
        IsError = true;
        StatusMessage = message;
    }
}
