using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Models;
using Phloem.Services;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;
using Phloem.Settings;

namespace Phloem.ViewModels;

/// <summary>One country's range on a dish name card: "🇬🇭 GH₵40 – GH₵90" or "🇳🇬 not set".</summary>
public sealed class PriceRangeChip
{
    public required CountryOption Country { get; init; }
    public DishPriceRangeDto? Range { get; init; }
    public bool IsSet => Range is not null;
    public string Text => Range is { } r
        ? $"{Country.FlagEmoji} {OrderService.SymbolOf(r.Currency)}{r.MinPrice:0.##} – {OrderService.SymbolOf(r.Currency)}{r.MaxPrice:0.##}"
        : $"{Country.FlagEmoji} not set";
}

/// <summary>One dish name card.</summary>
public sealed class DishNameCard
{
    public required AdminDishCatalogDto Item { get; init; }
    public required IReadOnlyList<PriceRangeChip> Ranges { get; init; }
    public string Name => Item.Name;
    public string GroupText => $"{DishCatalogService.GroupText(Item.Group)} · {DishCatalogService.CategoryText(Item.Category)}";

    /// <summary>Missing a range in some country: sellers there can't sell it.</summary>
    public bool NeedsPrice => Ranges.Any(r => !r.IsSet);

    /// <summary>How the name is said, in short: "WAH-chay · 🔊", "🔊 recorded", or "Add how it's said".</summary>
    public string PronunciationText
    {
        get
        {
            var guide = Item.PronunciationGuide;
            var sound = !string.IsNullOrEmpty(Item.PronunciationAudioUrl);
            return (string.IsNullOrEmpty(guide), sound) switch
            {
                (false, true) => $"{guide} · 🔊",
                (false, false) => guide!,
                (true, true) => "🔊 recorded",
                _ => "Add how it's said"
            };
        }
    }
}

/// <summary>
/// Admin → Dish names &amp; price ranges (route "admindishes"): every approved dish name with its allowed price range in
/// each country. Tap a name, pick a country, set the lowest and highest price. A name with no range in a country
/// can't be sold there; sellers whose dishes fall outside a new range are emailed to change their price.
/// </summary>
public partial class AdminDishCatalogViewModel : ObservableObject
{
    private readonly AdminService _admin;
    private IReadOnlyList<DishNameCard> _all = [];

    /// <summary>"Pronunciation ›" on a name: the written guide, other spellings and recording.</summary>
    [RelayCommand]
    private async Task OpenPronunciation(DishNameCard card) =>
        await Shell.Current.GoToAsync("admindishsound", new Dictionary<string, object> { ["DishName"] = card.Item });

    public AdminDishCatalogViewModel(AdminService admin, PagingFactory paging)
    {
        _admin = admin;
        Pager = paging.Create(PagedLists.AdminDishNames, () =>
        {
            ShowPage();
            return Task.CompletedTask;
        });
    }

    /// <summary>The page of names being shown.</summary>
    public ObservableCollection<DishNameCard> Names { get; } = new();

    public Paging Pager { get; }

    public IReadOnlyList<CountryOption> Countries => CountryOption.All;

    [ObservableProperty]
    public partial string Search { get; set; } = string.Empty;

    /// <summary>Only names missing a range somewhere.</summary>
    [ObservableProperty]
    public partial bool NeedsPriceOnly { get; set; }

    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial string Message { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsError { get; set; }

    /// <summary>"3 names have no price in some country".</summary>
    [ObservableProperty] public partial string SummaryText { get; set; } = string.Empty;

    // ---------- Range form ----------
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditing), nameof(FormTitle))]
    public partial DishNameCard? Editing { get; set; }

    public bool IsEditing => Editing is not null;
    public string FormTitle => Editing is null ? string.Empty : $"Price range for {Editing.Name}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrencyText))]
    public partial CountryOption? FormCountry { get; set; }

    public string CurrencyText => FormCountry is null ? string.Empty : $"In {FormCountry.CurrencySymbol} ({FormCountry.Name})";

    [ObservableProperty] public partial string MinPrice { get; set; } = string.Empty;
    [ObservableProperty] public partial string MaxPrice { get; set; } = string.Empty;

    partial void OnSearchChanged(string value) => Filter();
    partial void OnNeedsPriceOnlyChanged(bool value) => Filter();

    /// <summary>When the country changes in the form, show that country's current range.</summary>
    partial void OnFormCountryChanged(CountryOption? value)
    {
        var range = Editing?.Item.PriceRanges.FirstOrDefault(r => r.Country == value?.ApiCountry);
        MinPrice = range?.MinPrice.ToString("0.##") ?? string.Empty;
        MaxPrice = range?.MaxPrice.ToString("0.##") ?? string.Empty;
    }

    /// <summary>Called each time the page appears.</summary>
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var result = await _admin.GetDishNamesAsync();
            if (result.IsFailure)
            {
                ShowError(result.Error!.DisplayMessage);
                return;
            }

            _all = result.Value!
                .OrderBy(i => i.Group).ThenBy(i => i.Name)
                .Select(i => new DishNameCard
                {
                    Item = i,
                    Ranges = CountryOption.All
                        .Select(c => new PriceRangeChip
                        {
                            Country = c,
                            Range = i.PriceRanges.FirstOrDefault(r => r.Country == c.ApiCountry)
                        })
                        .ToList()
                })
                .ToList();

            var missing = _all.Count(n => n.NeedsPrice);
            SummaryText = missing == 0
                ? $"{_all.Count} names, all priced in every country."
                : $"{_all.Count} names · {missing} without a price in some country (sellers there can't use them).";
            Filter(keepPage: true);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task Refresh() => await LoadAsync();

    private void Filter(bool keepPage = false)
    {
        if (!keepPage) Pager.Reset();
        Pager.Update(Filtered().Count());
        if (Pager.Page > Pager.TotalPages) Pager.Reset();
        ShowPage();
    }

    private IEnumerable<DishNameCard> Filtered() =>
        _all.Where(n => (!NeedsPriceOnly || n.NeedsPrice)
                        && (string.IsNullOrWhiteSpace(Search)
                            || n.Name.Contains(Search.Trim(), StringComparison.OrdinalIgnoreCase)));

    private void ShowPage()
    {
        Names.Clear();
        foreach (var card in Filtered().Skip((Pager.Page - 1) * Pager.PageSize).Take(Pager.PageSize))
            Names.Add(card);
    }

    // ---------- Range form ----------
    [RelayCommand]
    private void Edit(DishNameCard card)
    {
        Editing = card;
        // Start with the first country that has no range, else the first country
        FormCountry = card.Ranges.FirstOrDefault(r => !r.IsSet)?.Country ?? Countries.FirstOrDefault();
        OnFormCountryChanged(FormCountry);
        Message = string.Empty;
    }

    [RelayCommand]
    private void CancelEdit() => Editing = null;

    [RelayCommand]
    private void ChooseCountry(CountryOption country) => FormCountry = country;

    [RelayCommand]
    private async Task SaveRange()
    {
        if (Editing is not { } card || FormCountry is null) return;

        if (InternationalDeliveryViewModel.ParseDecimal(MinPrice) is not { } min ||
            InternationalDeliveryViewModel.ParseDecimal(MaxPrice) is not { } max || min <= 0 || max < min)
        {
            ShowError("Enter the lowest and highest price, e.g. 40 and 90 (highest at least the lowest).");
            return;
        }

        IsLoading = true;
        try
        {
            var result = await _admin.SetPriceRangeAsync(card.Item.Id, FormCountry.ApiCountry, min, max);
            if (result.IsFailure)
            {
                ShowError(result.Error!.DisplayMessage);
                return;
            }

            var needUpdate = result.Value;
            var name = card.Name;
            var country = FormCountry.Name;
            Editing = null;
            await LoadAsync();
            IsError = false;
            Message = needUpdate == 0
                ? $"{name}: price range saved for {country}."
                : $"{name}: range saved for {country}. {needUpdate} dish(es) are now outside it; their sellers were emailed to change the price.";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ShowError(string message)
    {
        IsError = true;
        Message = message;
    }
}
