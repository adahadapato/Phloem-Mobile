using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Models;
using Phloem.Services;
using Phloem.Services.Api;
using Phloem.Services.Api.Contracts.Commands;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.ViewModels;

/// <summary>One route card (e.g. Ghana → UK) on Shipping routes.</summary>
public sealed class RouteRow
{
    public required AdminShipmentRouteDto Route { get; init; }
    public string Title => ShipmentText.Route(Route.FromCountry, Route.ToCountry);
    public bool IsActive => Route.IsActive;
    public string StatusText => Route.IsActive ? "On · customers can book" : "Off · customers can't see it";
    public string ToggleText => Route.IsActive ? "Switch off" : "Switch on";
    public string DropOffText => $"{Route.DropOffAddress}\n{Route.DropOffHours}";
    public bool HasNoRates => Route.Rates.Count == 0;
    public IReadOnlyList<RateRow> Rates => Route.Rates.Select(r => new RateRow { Route = Route, Rate = r }).ToList();

    /// <summary>"Size: L × W × H ÷ 5,000 · Fragile: £10.00 · Home pickup: off · Hub collection: off".</summary>
    public string OptionsText =>
        $"Size weight: L × W × H ÷ {Route.VolumeDivisor:N0}\n" +
        $"Fragile handling: {(Route.FragileCharge > 0 ? ShipmentText.Money(Route.FragileCharge, Route.Currency) : "free")}\n" +
        $"Home pickup: {(Route.HomePickupCharge is { } p ? ShipmentText.Money(p, Route.Currency) : "off")}\n" +
        $"Recipient collects from our hub: {(Route.CollectionHubAddress is { } a ? $"{a} ({Route.CollectionHubHours})" : "off")}";

    /// <summary>Door delivery charge per zone of the destination country.</summary>
    public IReadOnlyList<ZoneChargeRow> Zones =>
        (Route.ZoneCharges ?? []).Select(z => new ZoneChargeRow { Route = Route, Zone = z }).ToList();

    public bool HasZones => Route.ZoneCharges is { Count: > 0 };
    public bool HasNoZones => !HasZones;
}

/// <summary>One zone's door delivery charge on a route card ("South-West  £20.00").</summary>
public sealed class ZoneChargeRow
{
    public required AdminShipmentRouteDto Route { get; init; }
    public required AdminZoneChargeDto Zone { get; init; }
    public string Title => Zone.ZoneName;
    public string AmountText => Zone.DoorDeliveryCharge is { } c ? ShipmentText.Money(c, Route.Currency) : "not set (free)";
}

/// <summary>One speed's price on a route card.</summary>
public sealed class RateRow
{
    public required AdminShipmentRouteDto Route { get; init; }
    public required ShipmentRateDto Rate { get; init; }
    public string Title => ShipmentText.Speed(Rate.Speed);
    public string Details =>
        $"{ShipmentText.Money(Rate.BaseCharge, Route.Currency)} + {ShipmentText.Money(Rate.PricePerKg, Route.Currency)}/kg" +
        $" · {ShipmentText.Days(Rate.MinTransitDays, Rate.MaxTransitDays)}";
}

/// <summary>A speed in the rate form's picker.</summary>
public sealed class SpeedOption
{
    public required DeliverySpeed Value { get; init; }
    public string Name => ShipmentText.Speed(Value);
}

/// <summary>
/// Admin → Shipping routes (route "adminroutes"), on the API:
/// <list type="number">
///   <item><description><b>New route</b> (e.g. Ghana → UK) with its drop-off hub and opening hours. It starts
///   switched OFF.</description></item>
///   <item><description><b>Rates:</b> per speed, a base charge + price per kg (in the sending country's currency)
///   and the transit days.</description></item>
///   <item><description><b>Parcel options:</b> the size divisor, fragile charge, home pickup (on/off + charge) and
///   the destination hub where recipients can collect.</description></item>
///   <item><description><b>Door delivery by zone:</b> a charge for each zone of the destination country (zones
///   themselves are set on the website, Admin → Shipping → Zones &amp; towns).</description></item>
///   <item><description><b>Switch on</b> once it has a rate: customers can then book it on Send a parcel.</description></item>
/// </list>
/// </summary>
public partial class AdminShipmentRoutesViewModel : ObservableObject
{
    private readonly ShipmentRouteService _routes;

    public AdminShipmentRoutesViewModel(ShipmentRouteService routes)
    {
        _routes = routes;
    }

    public ObservableCollection<RouteRow> Routes { get; } = new();
    public IReadOnlyList<CountryOption> Countries => CountryOption.All;
    public IReadOnlyList<SpeedOption> Speeds { get; } =
        Enum.GetValues<DeliverySpeed>().Select(s => new SpeedOption { Value = s }).ToList();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsError { get; set; }

    // ---------- Route form (new route, or a route's drop-off) ----------

    [ObservableProperty]
    public partial bool IsEditingRoute { get; set; }

    /// <summary>True when changing an existing route's drop-off (its countries can't change).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNewRoute), nameof(RouteFormTitle))]
    public partial AdminShipmentRouteDto? EditingRoute { get; set; }

    public bool IsNewRoute => EditingRoute is null;
    public string RouteFormTitle => EditingRoute is { } r
        ? $"Drop-off for {ShipmentText.Route(r.FromCountry, r.ToCountry)}"
        : "New route";

    [ObservableProperty]
    public partial CountryOption? FromCountry { get; set; }

    [ObservableProperty]
    public partial CountryOption? ToCountry { get; set; }

    [ObservableProperty]
    public partial string DropOffAddress { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string DropOffHours { get; set; } = string.Empty;

    // ---------- Rate form ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingRate), nameof(RateFormTitle))]
    public partial AdminShipmentRouteDto? RateRoute { get; set; }

    public bool IsEditingRate => RateRoute is not null;
    public string RateFormTitle => RateRoute is { } r
        ? $"Rate for {ShipmentText.Route(r.FromCountry, r.ToCountry)} (in {r.Currency})"
        : string.Empty;

    [ObservableProperty]
    public partial SpeedOption? RateSpeed { get; set; }

    [ObservableProperty]
    public partial string BaseCharge { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PricePerKg { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MinDays { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string MaxDays { get; set; } = string.Empty;

    // ---------- Parcel options form ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditingOptions), nameof(OptionsFormTitle))]
    public partial AdminShipmentRouteDto? OptionsRoute { get; set; }

    public bool IsEditingOptions => OptionsRoute is not null;
    public string OptionsFormTitle => OptionsRoute is { } r
        ? $"Parcel options for {ShipmentText.Route(r.FromCountry, r.ToCountry)} (in {r.Currency})"
        : string.Empty;

    [ObservableProperty]
    public partial string VolumeDivisor { get; set; } = "5000";

    [ObservableProperty]
    public partial string FragileCharge { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool OffersPickup { get; set; }

    [ObservableProperty]
    public partial string PickupCharge { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CollectionHubAddress { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string CollectionHubHours { get; set; } = string.Empty;

    // ═════════════════════════════ Loading ═════════════════════════════

    /// <summary>Called each time the page appears (and after every change).</summary>
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var result = await _routes.GetAllAsync();
            if (result.IsFailure)
            {
                ShowError(result.Error!.DisplayMessage);
                return;
            }

            Routes.Clear();
            foreach (var r in result.Value!.OrderBy(r => r.FromCountry).ThenBy(r => r.ToCountry))
                Routes.Add(new RouteRow { Route = r });

            if (Routes.Count == 0 && !IsEditingRoute)
                ShowInfo("No routes yet. Tap \"New route\" to set up Ghana → UK.");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(Countries));
        }
    }

    [RelayCommand]
    private async Task Refresh() => await LoadAsync();

    // ═════════════════════════════ Route ═════════════════════════════

    [RelayCommand]
    private void NewRoute()
    {
        RateRoute = null;
        OptionsRoute = null;
        EditingRoute = null;
        FromCountry = Countries.FirstOrDefault(c => c.ApiCountry == Country.Ghana) ?? Countries.FirstOrDefault();
        ToCountry = Countries.FirstOrDefault(c => c.ApiCountry == Country.UnitedKingdom) ?? Countries.LastOrDefault();
        DropOffAddress = DropOffHours = string.Empty;
        IsEditingRoute = true;
        Message = string.Empty;
    }

    [RelayCommand]
    private void EditDropOff(RouteRow row)
    {
        RateRoute = null;
        OptionsRoute = null;
        EditingRoute = row.Route;
        DropOffAddress = row.Route.DropOffAddress;
        DropOffHours = row.Route.DropOffHours;
        IsEditingRoute = true;
        Message = string.Empty;
    }

    [RelayCommand]
    private void CancelRoute() => IsEditingRoute = false;

    [RelayCommand]
    private async Task SaveRoute()
    {
        if (string.IsNullOrWhiteSpace(DropOffAddress) || string.IsNullOrWhiteSpace(DropOffHours))
        {
            ShowError("Enter the drop-off address and its opening hours, e.g. \"Mon–Sat, 9am–5pm\".");
            return;
        }

        ApiResult result;
        if (EditingRoute is { } existing)
        {
            result = await _routes.SetDropOffAsync(existing.FromCountry, existing.ToCountry,
                new DropOffRequest(DropOffAddress.Trim(), DropOffHours.Trim()));
        }
        else
        {
            if (FromCountry is null || ToCountry is null || FromCountry.ApiCountry == ToCountry.ApiCountry)
            {
                ShowError("Choose two different countries.");
                return;
            }

            result = await _routes.CreateAsync(new CreateShipmentRouteCommand(
                FromCountry.ApiCountry, ToCountry.ApiCountry, DropOffAddress.Trim(), DropOffHours.Trim()));
        }

        if (result.IsFailure)
        {
            ShowError(result.Error!.DisplayMessage);
            return;
        }

        var created = EditingRoute is null;
        IsEditingRoute = false;
        await LoadAsync();
        ShowInfo(created
            ? "Route created (switched off). Now add a rate for each speed, then switch it on."
            : "Drop-off saved.");
    }

    [RelayCommand]
    private async Task ToggleActive(RouteRow row)
    {
        var turnOn = !row.Route.IsActive;
        if (!turnOn)
        {
            var ok = await Shell.Current.DisplayAlertAsync("Switch off this route?",
                $"{row.Title}\nCustomers won't be able to book it. Parcels already booked carry on.", "Switch off",
                "Keep it on");
            if (!ok) return;
        }

        var result = await _routes.SetActiveAsync(row.Route.FromCountry, row.Route.ToCountry, turnOn);
        if (result.IsFailure)
        {
            ShowError(result.Error!.DisplayMessage);   // e.g. "add a rate first"
            return;
        }

        await LoadAsync();
        ShowInfo(turnOn ? $"{row.Title} is on: customers can book it now." : $"{row.Title} is off.");
    }

    // ═════════════════════════════ Rates ═════════════════════════════

    [RelayCommand]
    private void AddRate(RouteRow row)
    {
        IsEditingRoute = false;
        OptionsRoute = null;
        RateRoute = row.Route;
        RateSpeed = Speeds.FirstOrDefault(s => row.Route.Rates.All(r => r.Speed != s.Value)) ?? Speeds[0];
        BaseCharge = PricePerKg = MinDays = MaxDays = string.Empty;
        Message = string.Empty;
    }

    /// <summary>Tapping a rate fills the form with it, to change it.</summary>
    [RelayCommand]
    private void EditRate(RateRow rate)
    {
        IsEditingRoute = false;
        OptionsRoute = null;
        RateRoute = rate.Route;
        RateSpeed = Speeds.First(s => s.Value == rate.Rate.Speed);
        BaseCharge = rate.Rate.BaseCharge.ToString("0.##");
        PricePerKg = rate.Rate.PricePerKg.ToString("0.##");
        MinDays = rate.Rate.MinTransitDays.ToString();
        MaxDays = rate.Rate.MaxTransitDays.ToString();
        Message = string.Empty;
    }

    [RelayCommand]
    private void CancelRate() => RateRoute = null;

    [RelayCommand]
    private async Task SaveRate()
    {
        if (RateRoute is not { } route || RateSpeed is null) return;

        if (InternationalDeliveryViewModel.ParseDecimal(BaseCharge) is not { } baseCharge ||
            InternationalDeliveryViewModel.ParseDecimal(PricePerKg) is not { } perKg ||
            !int.TryParse(MinDays, out var min) || !int.TryParse(MaxDays, out var max))
        {
            ShowError("Enter the base charge, price per kg, and the fewest and most days it takes.");
            return;
        }

        var result = await _routes.SetRateAsync(route.FromCountry, route.ToCountry, RateSpeed.Value,
                                                new ShipmentRateRequest(baseCharge, perKg, min, max));
        if (result.IsFailure)
        {
            ShowError(result.Error!.DisplayMessage);
            return;
        }

        RateRoute = null;
        await LoadAsync();
        ShowInfo($"{RateSpeed.Name} rate saved.");
    }

    [RelayCommand]
    private async Task RemoveRate(RateRow rate)
    {
        var ok = await Shell.Current.DisplayAlertAsync("Remove this speed?",
            $"{rate.Title} on {ShipmentText.Route(rate.Route.FromCountry, rate.Route.ToCountry)}", "Remove", "Keep");
        if (!ok) return;

        var result = await _routes.RemoveRateAsync(rate.Route.FromCountry, rate.Route.ToCountry, rate.Rate.Speed);
        if (result.IsFailure)
        {
            ShowError(result.Error!.DisplayMessage);
            return;
        }

        await LoadAsync();
        ShowInfo($"{rate.Title} removed.");
    }

    // ═════════════════════════════ Parcel options ═════════════════════════════

    [RelayCommand]
    private void EditOptions(RouteRow row)
    {
        IsEditingRoute = false;
        RateRoute = null;
        var r = row.Route;
        OptionsRoute = r;
        VolumeDivisor = r.VolumeDivisor.ToString();
        FragileCharge = r.FragileCharge.ToString("0.##");
        OffersPickup = r.HomePickupCharge is not null;
        PickupCharge = r.HomePickupCharge?.ToString("0.##") ?? string.Empty;
        CollectionHubAddress = r.CollectionHubAddress ?? string.Empty;
        CollectionHubHours = r.CollectionHubHours ?? string.Empty;
        Message = string.Empty;
    }

    [RelayCommand]
    private void CancelOptions() => OptionsRoute = null;

    [RelayCommand]
    private async Task SaveOptions()
    {
        if (OptionsRoute is not { } route) return;

        if (!int.TryParse(VolumeDivisor, out var divisor) || divisor is < 1000 or > 10000)
        {
            ShowError("The size divisor must be a whole number from 1000 to 10000 (usually 5000).");
            return;
        }
        if (InternationalDeliveryViewModel.ParseDecimal(string.IsNullOrWhiteSpace(FragileCharge) ? "0" : FragileCharge) is not { } fragile)
        {
            ShowError("Enter the fragile charge (0 = free).");
            return;
        }
        decimal? pickup = null;
        if (OffersPickup)
        {
            if (InternationalDeliveryViewModel.ParseDecimal(PickupCharge) is not { } p)
            {
                ShowError("Enter the home pickup charge (0 or more).");
                return;
            }
            pickup = p;
        }
        if (string.IsNullOrWhiteSpace(CollectionHubAddress) != string.IsNullOrWhiteSpace(CollectionHubHours))
        {
            ShowError("Give the collection hub's address AND opening hours, or leave both empty.");
            return;
        }

        var result = await _routes.SetParcelOptionsAsync(route.FromCountry, route.ToCountry, new ParcelOptionsRequest(
            divisor, fragile, pickup,
            string.IsNullOrWhiteSpace(CollectionHubAddress) ? null : CollectionHubAddress.Trim(),
            string.IsNullOrWhiteSpace(CollectionHubHours) ? null : CollectionHubHours.Trim()));
        if (result.IsFailure)
        {
            ShowError(result.Error!.DisplayMessage);
            return;
        }

        OptionsRoute = null;
        await LoadAsync();
        ShowInfo("Parcel options saved. New quotes use them at once.");
    }

    // ═════════════════════════════ Door delivery by zone ═════════════════════════════

    /// <summary>Tap a zone: type its door delivery charge (blank = remove it, so door delivery there is free).</summary>
    [RelayCommand]
    private async Task EditZoneCharge(ZoneChargeRow row)
    {
        var text = await Shell.Current.DisplayPromptAsync($"Door delivery · {row.Title}",
            $"Charge in {row.Route.Currency} for delivery to a door in {row.Title}. Leave empty for no charge.",
            "Save", "Cancel", placeholder: "e.g. 20", keyboard: Keyboard.Numeric,
            initialValue: row.Zone.DoorDeliveryCharge?.ToString("0.##") ?? string.Empty);
        if (text is null) return;

        ApiResult result;
        if (string.IsNullOrWhiteSpace(text))
        {
            result = await _routes.RemoveZoneChargeAsync(row.Route.FromCountry, row.Route.ToCountry, row.Zone.ZoneId);
        }
        else if (InternationalDeliveryViewModel.ParseDecimal(text) is { } charge && charge >= 0)
        {
            result = await _routes.SetZoneChargeAsync(row.Route.FromCountry, row.Route.ToCountry, row.Zone.ZoneId, charge);
        }
        else
        {
            ShowError("Enter the charge as a number, e.g. 20.");
            return;
        }

        if (result.IsFailure)
        {
            ShowError(result.Error!.DisplayMessage);
            return;
        }

        await LoadAsync();
        ShowInfo(string.IsNullOrWhiteSpace(text) ? $"Door delivery to {row.Title} is now free." : $"Door delivery to {row.Title} saved.");
    }

    private void ShowError(string message)
    {
        IsError = true;
        Message = message;
    }

    private void ShowInfo(string message)
    {
        IsError = false;
        Message = message;
    }
}
