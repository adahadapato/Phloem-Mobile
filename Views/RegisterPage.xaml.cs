using Phloem.ViewModels;

namespace Phloem.Views;

public partial class RegisterPage : ContentPage
{
    readonly RegisterViewModel _vm;

    public RegisterPage(RegisterViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // Show the flag of the country picked on Home, and "Continue with Google" when the API has it switched on
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        var cart = IPlatformApplication.Current?.Services.GetService<Phloem.Services.CartService>();
        FlagLabel.Text = cart?.SelectedCountry?.FlagEmoji ?? "🌍";

        await _vm.LoadSignInOptionsAsync();
    }

    // ☰ opens the side menu
    void OnMenuTapped(object? sender, TappedEventArgs e)
    {
        if (Shell.Current is not null)
            Shell.Current.FlyoutIsPresented = true;
    }

    // "Already have an account? Log in" → Account tab
    async void OnLogInTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("//account/login");

    // Buyer | Seller choice
    void OnBuyerTapped(object? sender, TappedEventArgs e) => _vm.IsSeller = false;
    void OnSellerTapped(object? sender, TappedEventArgs e) => _vm.IsSeller = true;
}
