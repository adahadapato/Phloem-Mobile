using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Models;
using Phloem.Services;
using Phloem.Services.Api;
using Phloem.Services.Api.Contracts.Enums;
using Phloem.Services.Voice;
using Phloem.Settings;

namespace Phloem.ViewModels;

/// <summary>
/// Browse &amp; Order (Explore tab): dishes from the Phloem API (<see cref="DishService"/>) by country, category,
/// search text and sort order, or one shop's menu when opened from "View Shop". Shown a page at a time
/// (<see cref="Pager"/>); a new search or filter goes back to page 1.
/// </summary>
public partial class FoodListingViewModel : ObservableObject
{
    private readonly DishService _dishes;
    private readonly CartService _cart;
    private readonly VoiceSearch _voice;
    private CancellationTokenSource? _load;

    public ObservableCollection<CountryOption> Countries { get; } = new(CountryOption.All);
    public ObservableCollection<string> DeliveryTimes { get; } = new() { "ASAP (30-45 min)", "Today, Evening", "Tomorrow" };
    public ObservableCollection<Dish> Dishes { get; } = new();

    /// <summary>◀ Page x of y ▶ and "Show n" under the dishes.</summary>
    public Paging Pager { get; }

    [ObservableProperty]
    public partial CountryOption SelectedCountry { get; set; }

    [ObservableProperty]
    public partial string SelectedDeliveryTime { get; set; }

    [ObservableProperty]
    public partial int CartCount { get; set; }

    [ObservableProperty]
    public partial decimal CartTotal { get; set; }

    [ObservableProperty]
    public partial string? FilterVendorId { get; set; }

    [ObservableProperty]
    public partial string FilterVendorName { get; set; } = string.Empty;

    /// <summary>NEW: text typed in the search bar.</summary>
    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    /// <summary>NEW: selected chip: "All", "Local Meals", "Snacks" or "Drinks".</summary>
    [ObservableProperty]
    public partial string SelectedCategory { get; set; } = "All";

    /// <summary>NEW: "Rating", "PriceLow" or "PriceHigh" (set from the Filter link).</summary>
    [ObservableProperty]
    public partial string SortBy { get; set; } = "Rating";

    public bool IsFilteredByVendor => !string.IsNullOrEmpty(FilterVendorId);

    /// <summary>True when there's a message to show instead of dishes (nothing matches, or no connection).</summary>
    public bool IsEmpty => !string.IsNullOrEmpty(EmptyMessage);

    /// <summary>Why no dishes are shown: nothing matches, or the API couldn't be reached.</summary>
    [ObservableProperty]
    public partial string EmptyMessage { get; set; } = string.Empty;

    /// <summary>True while dishes are loading.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>The basket total with the country's currency, e.g. "GH₵85.00".</summary>
    public string CartTotalText => $"{_cart.SelectedCountry?.CurrencySymbol}{CartTotal:N2}";

    partial void OnEmptyMessageChanged(string value) => OnPropertyChanged(nameof(IsEmpty));
    partial void OnCartTotalChanged(decimal value) => OnPropertyChanged(nameof(CartTotalText));

    public FoodListingViewModel(DishService dishes, CartService cart, PagingFactory paging, VoiceSearch voice)
    {
        _voice = voice;
        _dishes = dishes;
        _cart = cart;
        Pager = paging.Create(PagedLists.Dishes, () => LoadDishesAsync(debounce: false));
        SelectedCountry = _cart.SelectedCountry;
        SelectedDeliveryTime = DeliveryTimes[0];
        _cart.Items.CollectionChanged += (_, _) => RefreshCartSummary();
        RefreshDishes();
        RefreshCartSummary();
    }


    // ---------- Voice search (the 🎤 on the search bar) ----------

    /// <summary>True when the phone can search by voice (the 🎤 shows).</summary>
    public bool CanUseVoice => _voice.IsSupported;

    /// <summary>True while listening (the 🎤 turns green and its ring pulses).</summary>
    [ObservableProperty]
    public partial bool IsListening { get; set; }

    /// <summary>
    /// The 🎤: listens and fills the search box as the person speaks; the list follows the words (the API's
    /// "sounds-like" search finds dishes whose names were misheard). Tapping again while listening stops early.
    /// </summary>
    [RelayCommand]
    private async Task Voice()
    {
        if (IsListening)
        {
            _voice.StopListening();
            return;
        }

        IsListening = true;
        try
        {
            var hints = Dishes.Select(d => d.Name).Distinct().ToList();
            var heard = await _voice.ListenAsync(SelectedCountry?.ApiCountry, hints, words => SearchText = words);

            if (heard.Problem is { } problem)
            {
                await Shell.Current.DisplayAlertAsync("Voice search", problem, "OK");
                return;
            }
            if (!string.IsNullOrWhiteSpace(heard.Text)) SearchText = heard.Text;
        }
        catch (Exception ex)
        {
            // Never let the 🎤 close the app (an async command's error would)
            await Shell.Current.DisplayAlertAsync("Voice search", VoiceSearch.Problem(ex), "OK");
        }
        finally
        {
            IsListening = false;
        }
    }

    /// <summary>Called from the page when it's navigated to with a vendorId query parameter.</summary>
    public void SetVendorFilter(string? vendorId, string? vendorName)
    {
        FilterVendorId = vendorId;
        FilterVendorName = vendorName ?? string.Empty;
        OnPropertyChanged(nameof(IsFilteredByVendor));
        RefreshDishes();
    }

    [RelayCommand]
    private void ClearVendorFilter() => SetVendorFilter(null, null);

    /// <summary>NEW: the All / Local Meals / Snacks / Drinks chips.</summary>
    [RelayCommand]
    private void FilterCategory(string category) => SelectedCategory = category;

    partial void OnSelectedCountryChanged(CountryOption value)
    {
        _cart.SelectedCountry = value;
        RefreshDishes();
    }

    partial void OnSearchTextChanged(string value) => RefreshDishes();
    partial void OnSelectedCategoryChanged(string value) => RefreshDishes();
    partial void OnSortByChanged(string value) => RefreshDishes();

    /// <summary>NEW: called by the page each time it appears (country may have changed on Home).</summary>
    public void Reload()
    {
        if (SelectedCountry != _cart.SelectedCountry)
            SelectedCountry = _cart.SelectedCountry;   // triggers RefreshDishes (page 1)
        else
            _ = LoadDishesAsync(debounce: false);       // same page, fresh from the server
        RefreshCartSummary();
    }

    /// <summary>New search, filter, sort or country: back to page 1 and reload.</summary>
    private void RefreshDishes()
    {
        Pager?.Reset();
        _ = LoadDishesAsync(debounce: true);
    }

    /// <summary>Loads the current page from the API; a newer request (another letter typed) cancels the older one.</summary>
    /// <param name="debounce">Wait a moment first (the user may still be typing).</param>
    private async Task LoadDishesAsync(bool debounce)
    {
        if (Pager is null) return;   // still being created
        _load?.Cancel();
        var load = _load = new CancellationTokenSource();

        IsLoading = true;
        try
        {
            // Wait a moment while the user is still typing
            if (debounce) await Task.Delay(250, load.Token);

            var sort = SortBy switch
            {
                "PriceLow" => DishSort.PriceLow,
                "PriceHigh" => DishSort.PriceHigh,
                _ => DishSort.Rating
            };

            // A shop's menu shows all its dishes, whatever country is picked in the header
            var country = IsFilteredByVendor ? null : SelectedCountry?.ApiCountry;
            var result = await _dishes.SearchAsync(country, SelectedCategory, SearchText, FilterVendorId, sort,
                                                   Pager.Page, Pager.PageSize, load.Token);
            if (load.IsCancellationRequested) return;

            Dishes.Clear();
            if (result.IsFailure)
            {
                EmptyMessage = result.Error!.DisplayMessage;
                return;
            }

            foreach (var dish in result.Value!.Items) Dishes.Add(dish);
            Pager.Update(result.Value.TotalCount);
            EmptyMessage = Dishes.Count == 0 ? "No dishes match your search yet." : string.Empty;
        }
        catch (OperationCanceledException)
        {
            // A newer search replaced this one
        }
        catch (Exception)
        {
            // Never close the app because a list couldn't load: say so instead ("Try again" = pull/refresh)
            EmptyMessage = ApiError.Unexpected(0).DisplayMessage;
        }
        finally
        {
            if (_load == load) IsLoading = false;
        }
    }

    private void RefreshCartSummary()
    {
        CartCount = _cart.Items.Sum(i => i.Quantity);
        CartTotal = _cart.Subtotal;
        OnPropertyChanged(nameof(CartTotalText));
    }

    [RelayCommand]
    private void AddToCart(Dish dish)
    {
        _cart.AddDish(dish);
        RefreshCartSummary();
    }

    [RelayCommand]
    private async Task GoToCheckout()
    {
        if (_cart.Items.Count == 0) return;
        await AppNav.GoToCheckoutAsync();
    }
}
