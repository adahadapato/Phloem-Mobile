using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Models;
using Phloem.Services;
using Phloem.Services.Api;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.ViewModels;

/// <summary>One country's card on Admin → Pricing.</summary>
public sealed class PricingCountryCard
{
    public required CountryOption Country { get; init; }
    public CommissionDto? Commission { get; init; }
    public DeliveryPricingDto? Pricing { get; init; }

    public string Title => $"{Country.FlagEmoji}  {Country.Name}";
    public string CurrencyText => Country.Currency.ToString();

    /// <summary>True when buyers can order here: a fee range and at least one delivery time.</summary>
    public bool IsReady => Pricing is { Windows.Count: > 0 };
    public string StatusText => IsReady ? "Taking orders" : "Not set up";

    public string CommissionText => Commission is { IsSet: true } c ? $"{c.RatePercent:0.##}%" : "Not set (0%)";
    public bool CommissionIsSet => Commission is { IsSet: true };

    public bool HasPricing => Pricing is not null;
    public bool HasNoPricing => Pricing is null;
    public string FeeRangeText => Pricing is { } p ? $"{Money(p.MinShopFee)} – {Money(p.MaxShopFee)}" : string.Empty;
    public string SetUpNote => $"Set the delivery fee range first. Until then nobody can order in {Country.Name}.";

    /// <summary>Every delivery time, offered or not.</summary>
    public IReadOnlyList<PricingWindowRow> Windows =>
        Enum.GetValues<DeliveryWindow>()
            .Select(w => new PricingWindowRow
            {
                Card = this,
                Window = w,
                Charge = Pricing?.Windows.FirstOrDefault(x => x.Window == w)?.ExtraCharge
            })
            .ToList();

    /// <summary>"GH₵12.00".</summary>
    public string Money(decimal amount) => $"{Country.CurrencySymbol}{amount:N2}";
}

/// <summary>One delivery time on a country card ("This evening · + GH₵5.00").</summary>
public sealed class PricingWindowRow
{
    public required PricingCountryCard Card { get; init; }
    public required DeliveryWindow Window { get; init; }

    /// <summary>What it adds to an order, or null when it isn't offered.</summary>
    public decimal? Charge { get; init; }

    public string Title => DeliveryTimeOption.TextOf(Window);
    public bool IsOffered => Charge is not null;
    public bool IsNotOffered => Charge is null;
    public bool IsFree => Charge == 0;
    public string ChargeText => Charge switch
    {
        null => "Not offered",
        0 => "Free",
        { } c => $"+ {Card.Money(c)}"
    };
}

/// <summary>What the edit form at the top of the page is changing.</summary>
public enum PricingEditKind
{
    None,
    Commission,
    FeeRange,
    WindowCharge
}

/// <summary>
/// Admin → Pricing (route "adminpricing"), the same as the website's Admin → Pricing. One card per country (the
/// list grows with every new country; ones not set up yet come first):
/// <list type="bullet">
///   <item><description><b>Phloem's commission</b>: on the dishes of each delivered order (0–50%).</description></item>
///   <item><description><b>Shop delivery fee</b>: the range each shop's own fee must sit in. Shops outside a new
///   range are moved to the nearest end and emailed.</description></item>
///   <item><description><b>Delivery times</b>: which ones buyers can pick, and what each adds.</description></item>
/// </list>
/// "Change" opens one small form at the top of the page.
/// </summary>
public partial class AdminPricingViewModel(PricingService pricing) : ObservableObject
{
    /// <summary>Phloem's highest commission (the API's <c>Commission.MaxRatePercent</c>).</summary>
    private const decimal MaxCommission = 50;

    public ObservableCollection<PricingCountryCard> Cards { get; } = new();

    /// <summary>Raised when the form opens, so the page can scroll up to it.</summary>
    public event EventHandler? FormOpened;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsError { get; set; }

    // ---------- The edit form ----------

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing), nameof(HasSecondField), nameof(FormTitle), nameof(FormHint),
                              nameof(FirstLabel), nameof(SecondLabel), nameof(FirstPlaceholder))]
    public partial PricingEditKind EditKind { get; set; }

    private PricingCountryCard? _editingCard;
    private DeliveryWindow _editingWindow;

    public bool IsEditing => EditKind != PricingEditKind.None;
    public bool HasSecondField => EditKind == PricingEditKind.FeeRange;

    public string FormTitle => (EditKind, _editingCard) switch
    {
        (PricingEditKind.Commission, { } c) => $"Commission in {c.Country.Name}",
        (PricingEditKind.FeeRange, { } c) => $"Shop delivery fee in {c.Country.Name}",
        (PricingEditKind.WindowCharge, { } c) => $"\"{DeliveryTimeOption.TextOf(_editingWindow)}\" in {c.Country.Name}",
        _ => string.Empty
    };

    public string FormHint => EditKind switch
    {
        PricingEditKind.Commission =>
            $"0 to {MaxCommission:0}%, taken from the dishes of each delivered order. Delivery fees always go to the seller. Orders delivered from now on use the new rate.",
        PricingEditKind.FeeRange =>
            "Each shop sets its own fee inside this range. Shops outside it are moved to the nearest end and emailed.",
        PricingEditKind.WindowCharge => "What this delivery time adds to an order. 0 = free.",
        _ => string.Empty
    };

    public string FirstLabel => EditKind switch
    {
        PricingEditKind.Commission => "Commission (%)",
        PricingEditKind.FeeRange => $"Lowest fee ({_editingCard?.Country.CurrencySymbol})",
        PricingEditKind.WindowCharge => $"Extra charge ({_editingCard?.Country.CurrencySymbol})",
        _ => string.Empty
    };

    public string SecondLabel => $"Highest fee ({_editingCard?.Country.CurrencySymbol})";

    public string FirstPlaceholder => EditKind switch
    {
        PricingEditKind.Commission => "e.g. 10",
        PricingEditKind.FeeRange => "e.g. 5",
        _ => "e.g. 0"
    };

    [ObservableProperty]
    public partial string FirstValue { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SecondValue { get; set; } = string.Empty;

    // ═════════════════════════════ Loading ═════════════════════════════

    /// <summary>Called each time the page appears (and after every change).</summary>
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var commissionsTask = pricing.GetCommissionsAsync();
            var deliveryTask = pricing.GetDeliveryPricingAsync();
            var commissions = await commissionsTask;
            var delivery = await deliveryTask;

            if (commissions.IsFailure || delivery.IsFailure)
            {
                ShowError((commissions.Error ?? delivery.Error)?.DisplayMessage ?? "We couldn't load the pricing.");
                return;
            }

            var cards = CountryOption.All
                .Select(c => new PricingCountryCard
                {
                    Country = c,
                    Commission = commissions.Value!.FirstOrDefault(x => x.Country == c.ApiCountry),
                    Pricing = delivery.Value!.FirstOrDefault(x => x.Country == c.ApiCountry)
                })
                .OrderBy(c => c.HasPricing)   // not set up yet → first
                .ToList();

            Cards.Clear();
            foreach (var card in cards) Cards.Add(card);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Refresh() => await LoadAsync();

    // ═════════════════════════════ Opening the form ═════════════════════════════

    [RelayCommand]
    private void ChangeCommission(PricingCountryCard card) =>
        Open(card, PricingEditKind.Commission,
             card.Commission is { IsSet: true } c ? c.RatePercent.ToString("0.##") : string.Empty);

    [RelayCommand]
    private void ChangeFeeRange(PricingCountryCard card) =>
        Open(card, PricingEditKind.FeeRange,
             card.Pricing?.MinShopFee.ToString("0.##") ?? string.Empty,
             card.Pricing?.MaxShopFee.ToString("0.##") ?? string.Empty);

    [RelayCommand]
    private void ChangeWindow(PricingWindowRow row)
    {
        _editingWindow = row.Window;
        Open(row.Card, PricingEditKind.WindowCharge, (row.Charge ?? 0).ToString("0.##"));
    }

    private void Open(PricingCountryCard card, PricingEditKind kind, string first, string second = "")
    {
        _editingCard = card;
        FirstValue = first;
        SecondValue = second;
        EditKind = PricingEditKind.None;   // so the titles refresh even when the kind stays the same
        EditKind = kind;
        Message = string.Empty;
        FormOpened?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => EditKind = PricingEditKind.None;

    // ═════════════════════════════ Saving ═════════════════════════════

    [RelayCommand]
    private async Task Save()
    {
        if (_editingCard is not { } card || IsBusy) return;
        var country = card.Country;
        var first = InternationalDeliveryViewModel.ParseDecimal(FirstValue);

        switch (EditKind)
        {
            case PricingEditKind.Commission:
                if (first is not { } rate || rate > MaxCommission)
                {
                    ShowError($"Enter a commission between 0 and {MaxCommission:0}%.");
                    return;
                }
                await RunAsync(() => pricing.SetCommissionAsync(country.ApiCountry, rate),
                    $"Commission in {country.Name} is now {rate:0.##}%. It applies to orders delivered from now on.");
                break;

            case PricingEditKind.FeeRange:
                var second = InternationalDeliveryViewModel.ParseDecimal(SecondValue);
                if (first is not { } min || second is not { } max || max < min)
                {
                    ShowError("Enter a lowest fee of 0 or more, and a highest fee at least as high.");
                    return;
                }
                IsBusy = true;
                try
                {
                    var result = await pricing.SetShopFeeRangeAsync(country.ApiCountry, min, max);
                    if (result.IsFailure)
                    {
                        ShowError(result.Error!.DisplayMessage);
                        return;
                    }
                    var moved = result.Value;
                    EditKind = PricingEditKind.None;
                    await LoadAsync();
                    ShowInfo($"Shops in {country.Name} can now charge {card.Money(min)} – {card.Money(max)} for delivery." +
                             (moved > 0 ? $" {moved} shop{(moved == 1 ? "" : "s")} had their fee moved into the range and were emailed." : ""));
                }
                finally
                {
                    IsBusy = false;
                }
                break;

            case PricingEditKind.WindowCharge:
                if (first is not { } charge)
                {
                    ShowError("Enter 0 or more.");
                    return;
                }
                var name = DeliveryTimeOption.TextOf(_editingWindow);
                await RunAsync(() => pricing.SetWindowChargeAsync(country.ApiCountry, _editingWindow, charge),
                    $"\"{name}\" in {country.Name}: {(charge == 0 ? "free" : $"adds {card.Money(charge)}")}.");
                break;
        }
    }

    // ═════════════════════════════ Delivery times on / off ═════════════════════════════

    /// <summary>Starts offering a delivery time, free (the charge can then be changed).</summary>
    [RelayCommand]
    private async Task OfferWindow(PricingWindowRow row) =>
        await RunAsync(() => pricing.SetWindowChargeAsync(row.Card.Country.ApiCountry, row.Window, 0),
            $"\"{row.Title}\" is now offered in {row.Card.Country.Name}, free. Tap Change to add a charge.");

    /// <summary>Stops offering a delivery time (after asking).</summary>
    [RelayCommand]
    private async Task RemoveWindow(PricingWindowRow row)
    {
        var ok = await Shell.Current.DisplayAlertAsync("Stop offering this delivery time?",
            $"\"{row.Title}\" in {row.Card.Country.Name}. Orders already placed aren't affected.", "Stop offering", "Keep it");
        if (!ok) return;

        await RunAsync(() => pricing.RemoveWindowAsync(row.Card.Country.ApiCountry, row.Window),
            $"\"{row.Title}\" is no longer offered in {row.Card.Country.Name}.");
    }

    // ═════════════════════════════ Helpers ═════════════════════════════

    /// <summary>Runs a change, then reloads and says what happened (or shows the API's message).</summary>
    private async Task RunAsync(Func<Task<ApiResult>> action, string done)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var result = await action();
            if (result.IsFailure)
            {
                ShowError(result.Error!.DisplayMessage);
                return;
            }
            EditKind = PricingEditKind.None;
            await LoadAsync();
            ShowInfo(done);
        }
        finally
        {
            IsBusy = false;
        }
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
