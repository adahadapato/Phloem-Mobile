using Phloem.ViewModels;

namespace Phloem.Views;

public partial class FoodListingPage : ContentPage, IQueryAttributable
{
    private readonly FoodListingViewModel _vm;

    public FoodListingPage(FoodListingViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        BindingContext = vm;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // From Home's search (typed, or a picked prediction): show that search, all shops
        if (query.TryGetValue("search", out var search))
        {
            _vm.SetVendorFilter(null, null);
            _vm.SearchText = search?.ToString() ?? string.Empty;
            return;
        }

        if (query.TryGetValue("vendorId", out var vendorId))
        {
            query.TryGetValue("vendorName", out var vendorName);
            _vm.SetVendorFilter(vendorId?.ToString(), vendorName?.ToString());
        }
        else
        {
            // Navigated here without a vendor filter (e.g. from the flyout menu) - clear any previous filter.
            _vm.SetVendorFilter(null, null);
        }
    }

    // Paste these INSIDE the FoodListingPage class in FoodListingPage.xaml.cs,
    // next to your existing constructor and vendorId query code (keep that code as it is).

    FoodListingViewModel Vm => (FoodListingViewModel)BindingContext;

    // If your file already has an OnAppearing, put the Vm.Reload() line inside it instead.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        Vm.Reload();   // picks up a country changed on Home, and the latest basket
    }

    // ☰ opens the side menu
    void OnMenuTapped(object? sender, TappedEventArgs e)
    {
        if (Shell.Current is not null)
            Shell.Current.FlyoutIsPresented = true;
    }

    // Flag pill: change the app's country
    async void OnFlagTapped(object? sender, TappedEventArgs e)
    {
        var names = Vm.Countries.Select(c => $"{c.FlagEmoji}  {c.Name}").ToArray();
        var choice = await DisplayActionSheetAsync("Choose your country", "Cancel", null, names);
        var picked = Vm.Countries.FirstOrDefault(c => $"{c.FlagEmoji}  {c.Name}" == choice);
        if (picked is not null)
            Vm.SelectedCountry = picked;
    }

    // "Filter": choose how dishes are sorted
    async void OnFilterTapped(object? sender, TappedEventArgs e)
    {
        var choice = await DisplayActionSheetAsync("Sort dishes by", "Cancel", null,
            "Top rated", "Price: low to high", "Price: high to low");

        Vm.SortBy = choice switch
        {
            "Price: low to high" => "PriceLow",
            "Price: high to low" => "PriceHigh",
            "Top rated" => "Rating",
            _ => Vm.SortBy
        };
    }

    // Cart icon → Orders page (basket, Checkout / Continue shopping)
    async void OnCartTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("//orders");


}