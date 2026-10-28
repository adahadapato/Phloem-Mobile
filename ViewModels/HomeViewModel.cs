using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Models;
using Phloem.Services;
using Phloem.Services.Api;
using Phloem.Services.Api.Contracts.Enums;
using Phloem.Views;

namespace Phloem.ViewModels;
/// <summary>
/// View model for the home page, managing the display of
/// countries, categories, featured dishes, and top sellers.
/// </summary>
/// <remarks>Dishes and top sellers come from the Phloem API (<see cref="DishService"/>, <see cref="ShopService"/>),
/// for the country picked in the header.</remarks>
public partial class HomeViewModel : ObservableObject
{
    private readonly DishService _dishes;
    private readonly ShopService _shops;
    private readonly CartService _cart;
    private readonly SessionService _session;
    private readonly SuggestionService _suggestions;
    private CancellationTokenSource? _dishesLoad;
    private CancellationTokenSource? _shopsLoad;
    private CancellationTokenSource? _suggestionsLoad;
    private CancellationTokenSource? _predictionsLoad;

    /// <summary>
    /// Gets the collection of available countries.
    /// </summary>
    public ObservableCollection<CountryOption> Countries { get; } = new(CountryOption.All);

    /// <summary>
    /// Gets the collection of available categories.
    /// </summary>
    public ObservableCollection<string> Categories { get; } = new() { "Popular", "Local Meals", "Snacks", "Drinks" };

    /// <summary>
    /// Gets the collection of featured dishes.
    /// </summary>
    public ObservableCollection<Dish> FeaturedDishes { get; } = new();

    /// <summary>
    /// gets the collection of top sellers.
    /// </summary>
    public ObservableCollection<Vendor> TopSellers { get; } = new();

    // ---------- Search predictions (the list under the search bar, like the website's) ----------

    /// <summary>Dish names (then shops) containing what's typed in the search bar.</summary>
    public ObservableCollection<SearchPrediction> Predictions { get; } = new();

    [ObservableProperty]
    public partial bool HasPredictions { get; set; }

    // ---------- Suggestions (same as the website's Home) ----------

    /// <summary>"Order again": dishes I've bought, on sale now here (signed in only).</summary>
    public ObservableCollection<SuggestedDish> OrderAgain { get; } = new();

    /// <summary>"You might like" (with the reason on each card), or "Popular in Ghana".</summary>
    public ObservableCollection<SuggestedDish> ForYou { get; } = new();

    [ObservableProperty]
    public partial bool HasOrderAgain { get; set; }

    [ObservableProperty]
    public partial bool HasForYou { get; set; }

    /// <summary>"You might like", or "Popular in Ghana" for a new customer or someone signed out.</summary>
    [ObservableProperty]
    public partial string ForYouTitle { get; set; } = "You might like";

    [ObservableProperty]
    public partial CountryOption SelectedCountry { get; set; }

    [ObservableProperty]
    private string selectedCategory = "Popular";

    [ObservableProperty]
    private string searchText = string.Empty;

    [ObservableProperty]
    private int cartCount;

    /// <summary>Shown under "Popular Dishes" when nothing could be loaded or nothing matches.</summary>
    [ObservableProperty]
    public partial string DishesMessage { get; set; } = string.Empty;

    /// <summary>True while dishes are loading.</summary>
    [ObservableProperty]
    public partial bool IsLoading { get; set; }


    /// <summary>
    /// Initializes a new instance of the HomeViewModel class.
    /// </summary>
    /// <param name="dishes">Dishes from the API.</param>
    /// <param name="shops">Shops from the API.</param>
    /// <param name="cart">The service that manages the shopping cart.</param>
    /// <param name="session">The signed-in user (used by Sell Food and the suggestions).</param>
    /// <param name="suggestions">"Order again" / "You might like" / "Popular in …".</param>
    public HomeViewModel(DishService dishes, ShopService shops, CartService cart, SessionService session,
                         SuggestionService suggestions)
    {
        _dishes = dishes;
        _shops = shops;
        _cart = cart;
        _session = session;
        _suggestions = suggestions;
        SelectedCountry = _cart.SelectedCountry;
        _cart.Items.CollectionChanged += (_, _) => RefreshCartCount();
        // Signing in or out changes the rows ("Popular in …" ↔ "Order again" + "You might like")
        _session.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(RefreshSuggestions);
        RefreshDishes();
        RefreshTopSellers();
        RefreshCartCount();
    }

    private void RefreshCartCount() => CartCount = _cart.Items.Sum(i => i.Quantity);

    partial void OnSelectedCountryChanged(CountryOption value)
    {
        _cart.SelectedCountry = value;
        RefreshDishes();
        RefreshTopSellers();
        RefreshSuggestions();
    }

    partial void OnSelectedCategoryChanged(string value) => RefreshDishes();

    partial void OnSearchTextChanged(string value)
    {
        RefreshDishes();
        RefreshPredictions();
    }

    /// <summary>When true, the next text change came from picking a prediction: don't open the list again.</summary>
    private bool _picking;

    /// <summary>
    /// Reloads the predictions after a short pause in typing (a newer letter cancels the older request). Needs at
    /// least 2 characters; the list is hidden if it can't load (the search itself still works).
    /// </summary>
    private async void RefreshPredictions()
    {
        _predictionsLoad?.Cancel();
        var load = _predictionsLoad = new CancellationTokenSource();
        var text = SearchText.Trim();
        if (_picking || text.Length < 2)
        {
            _picking = false;
            HidePredictions();
            return;
        }

        try
        {
            await Task.Delay(200, load.Token);
            var result = await _dishes.SuggestAsync(text, SelectedCountry?.ApiCountry, load.Token);
            if (load.IsCancellationRequested) return;
            Predictions.Clear();
            if (result.IsSuccess)
                foreach (var s in result.Value!)
                    Predictions.Add(new SearchPrediction
                    {
                        Text = s.Text, Kind = s.Kind, DishCount = s.DishCount, ShopId = s.ShopId, Typed = text
                    });
            HasPredictions = Predictions.Count > 0;
        }
        catch (OperationCanceledException)
        {
            // Another letter was typed
        }
        catch (Exception)
        {
            HidePredictions();   // predictions are extra
        }
    }

    private void HidePredictions()
    {
        Predictions.Clear();
        HasPredictions = false;
    }

    /// <summary>
    /// A prediction was tapped: a dish name opens Explore searched for it; a shop opens its menu (like "View Shop").
    /// </summary>
    [RelayCommand]
    private async Task PickPrediction(SearchPrediction prediction)
    {
        _predictionsLoad?.Cancel();
        HidePredictions();
        if (prediction.IsShop && prediction.ShopId is { } shopId)
        {
            await Shell.Current.GoToAsync(
                $"//listing?vendorId={Uri.EscapeDataString(shopId.ToString())}&vendorName={Uri.EscapeDataString(prediction.Text)}");
            return;
        }

        if (SearchText != prediction.Text)
        {
            _picking = true;   // the text change below shouldn't open the list again
            SearchText = prediction.Text;
        }
        await Shell.Current.GoToAsync($"//listing?search={Uri.EscapeDataString(prediction.Text)}");
    }

    /// <summary>The keyboard's Search key: Explore searched for what was typed.</summary>
    [RelayCommand]
    private async Task SearchNow()
    {
        _predictionsLoad?.Cancel();
        HidePredictions();
        var text = SearchText.Trim();
        await Shell.Current.GoToAsync(text.Length == 0 ? "//listing" : $"//listing?search={Uri.EscapeDataString(text)}");
    }

    /// <summary>Reloads the dishes; a newer request (another letter typed) cancels the older one.</summary>
    private async void RefreshDishes()
    {
        _dishesLoad?.Cancel();
        var load = _dishesLoad = new CancellationTokenSource();

        IsLoading = true;
        try
        {
            // Wait a moment while the user is still typing
            await Task.Delay(250, load.Token);

            var result = await _dishes.SearchAsync(SelectedCountry?.ApiCountry, SelectedCategory, SearchText,
                                                   shopId: null, DishSort.Rating, page: 1,
                                                   pageSize: DishService.PageSize, ct: load.Token);
            if (load.IsCancellationRequested) return;

            FeaturedDishes.Clear();
            if (result.IsFailure)
            {
                DishesMessage = result.Error!.DisplayMessage;
                return;
            }

            foreach (var dish in result.Value!.Items) FeaturedDishes.Add(dish);
            DishesMessage = FeaturedDishes.Count == 0 ? "No dishes here yet. Try another country or category." : string.Empty;
        }
        catch (OperationCanceledException)
        {
            // A newer search replaced this one
        }
        catch (Exception)
        {
            // Never close the app because a list couldn't load: say so instead
            DishesMessage = ApiError.Unexpected(0).DisplayMessage;
        }
        finally
        {
            if (_dishesLoad == load) IsLoading = false;
        }
    }

    /// <summary>
    /// Reloads the suggestion rows for the chosen country (also each time Home appears, so a new order shows up).
    /// A row with nothing to show, or that couldn't load, is simply hidden: "Popular Dishes" is always there.
    /// </summary>
    public async void RefreshSuggestions()
    {
        _suggestionsLoad?.Cancel();
        var load = _suggestionsLoad = new CancellationTokenSource();
        try
        {
            if (SelectedCountry is null) return;
            var result = await _suggestions.GetForHomeAsync(SelectedCountry.ApiCountry, load.Token);
            if (load.IsCancellationRequested) return;
            if (result.IsFailure)
            {
                OrderAgain.Clear();
                ForYou.Clear();
            }
            else
            {
                var rows = result.Value!;
                Replace(OrderAgain, rows.OrderAgain);
                Replace(ForYou, rows.ForYou);
                ForYouTitle = rows.IsPersonal ? "You might like" : $"Popular in {SelectedCountry.Name}";
            }
        }
        catch (OperationCanceledException)
        {
            // The country changed again
        }
        catch (Exception)
        {
            // Suggestions are extra: hide them rather than show an error
            OrderAgain.Clear();
            ForYou.Clear();
        }
        finally
        {
            if (_suggestionsLoad == load)
            {
                HasOrderAgain = OrderAgain.Count > 0;
                HasForYou = ForYou.Count > 0;
            }
        }
    }

    private static void Replace(ObservableCollection<SuggestedDish> target, IReadOnlyList<SuggestedDish> items)
    {
        target.Clear();
        foreach (var item in items) target.Add(item);
    }

    /// <summary>Reloads "Top Sellers" for the chosen country.</summary>
    private async void RefreshTopSellers()
    {
        _shopsLoad?.Cancel();
        var load = _shopsLoad = new CancellationTokenSource();
        try
        {
            var result = await _shops.GetTopShopsAsync(SelectedCountry?.ApiCountry, ct: load.Token);
            if (load.IsCancellationRequested || result.IsFailure) return;

            TopSellers.Clear();
            foreach (var shop in result.Value!) TopSellers.Add(shop);
        }
        catch (OperationCanceledException)
        {
            // The country changed again
        }
        catch (Exception)
        {
            // Top Sellers is extra: if it can't load, the section simply stays as it is
        }
    }

    [RelayCommand]
    private void AddToCart(Dish dish)
    {
        _cart.AddDish(dish);
        RefreshCartCount();
    }

    [RelayCommand]
    private async Task GoToCheckout()
    {
        if (_cart.Items.Count == 0) return;
        await AppNav.GoToCheckoutAsync();
    }

    /// <summary>Buy Food → Browse &amp; Order (Explore tab).</summary>
    [RelayCommand]
    private async Task GoToBuy() => await Shell.Current.GoToAsync("//listing");

    /// <summary>"Order again" → "My orders ›": the Orders tab (basket and recent orders).</summary>
    [RelayCommand]
    private async Task GoToMyOrders() => await Shell.Current.GoToAsync("//orders");

    [RelayCommand]
    private async Task GoToVendors() => await Shell.Current.GoToAsync("//home/vendors");

    /// <summary>
    /// Sell Food:
    ///   not logged in                 → Log in
    ///   logged in and owns a shop     → Seller dashboard
    ///   logged in without a shop yet  → Become a seller
    /// </summary>
    [RelayCommand]
    private async Task GoToSell()
    {
        if (!_session.IsLoggedIn || _session.CurrentUser is null)
        {
            await Shell.Current.GoToAsync("//account/login");
            return;
        }

        await Shell.Current.GoToAsync(_session.HasShop ? "//home/dashboard" : "//home/sellersetup");
    }

    [RelayCommand]
    private async Task ViewShop(Vendor vendor) =>
        await Shell.Current.GoToAsync(
            $"//listing?vendorId={Uri.EscapeDataString(vendor.Id)}&vendorName={Uri.EscapeDataString(vendor.Name)}");

    [RelayCommand]
    private async Task GoToSendToUk() => await Shell.Current.GoToAsync("//home/international");
}
