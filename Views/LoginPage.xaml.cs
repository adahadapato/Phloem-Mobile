using Phloem.ViewModels;

namespace Phloem.Views;

public partial class LoginPage : ContentPage
{
    readonly LoginViewModel _vm;

    public LoginPage(LoginViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        var services = IPlatformApplication.Current?.Services;

        // Already logged in? This page shouldn't be showing; go back to My Account.
        var session = services?.GetService<Phloem.Services.SessionService>();
        if (session?.CurrentUser is not null)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        // Flag of the country picked on Home
        var cart = services?.GetService<Phloem.Services.CartService>();
        FlagLabel.Text = cart?.SelectedCountry?.FlagEmoji ?? "🌍";

        // "Continue with Google" only shows when the API has it switched on
        await _vm.LoadSignInOptionsAsync();
    }

    // ☰ opens the side menu
    void OnMenuTapped(object? sender, TappedEventArgs e)
    {
        if (Shell.Current is not null)
            Shell.Current.FlyoutIsPresented = true;
    }

    // Eye icon: show / hide the password
    void OnTogglePasswordTapped(object? sender, TappedEventArgs e)
        => PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
}