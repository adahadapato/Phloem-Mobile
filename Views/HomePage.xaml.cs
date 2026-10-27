using Phloem.ViewModels;

namespace Phloem.Views;

public partial class HomePage : ContentPage
{
    readonly HomeViewModel _vm;

    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // Back on Home (e.g. after ordering): refresh "Order again" / "You might like"
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.RefreshSuggestions();
    }

    // ☰ opens the side menu
    void OnMenuTapped(object? sender, TappedEventArgs e)
    {
        if (Shell.Current is not null)
            Shell.Current.FlyoutIsPresented = true;
    }

    // Flag pill: pick Ghana / Nigeria / UK
    async void OnCountryTapped(object? sender, TappedEventArgs e)
    {
        var names = _vm.Countries.Select(c => $"{c.FlagEmoji}  {c.Name}").ToArray();
        var choice = await DisplayActionSheetAsync("Choose your country", "Cancel", null, names);

        var picked = _vm.Countries.FirstOrDefault(c => $"{c.FlagEmoji}  {c.Name}" == choice);
        if (picked is not null)
            _vm.SelectedCountry = picked;
    }

    // Cart icon → Orders page (basket, Checkout / Continue shopping)
    async void OnCartTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("//orders");
}
