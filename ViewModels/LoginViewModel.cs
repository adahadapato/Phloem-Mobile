using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Services;

namespace Phloem.ViewModels;

/// <summary>
/// Represents the view model for user login, providing properties and commands for authentication and navigation.
/// </summary>
/// <remarks>
/// <para>Signs in through the Phloem API (<see cref="AuthService"/>). When the account has two-step verification on,
/// the 6-digit code (or a recovery code) is asked for in a pop-up, then sign-in finishes.</para>
/// <para>"Remember me" keeps the user signed in after the app is closed.</para>
/// <para>"Continue with Google" is an extra way in (shown only when the API has Google switched on); email and
/// password work exactly as before. The same two-step pop-up is used for both.</para>
/// </remarks>
public partial class LoginViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly CartService _cart;

    /// <summary>
    /// property for the user's email address, which is observable and can be bound to the UI.
    /// </summary>
    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    /// <summary>
    /// property for the user's password, which is observable and can be bound to the UI.
    /// </summary>
    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    /// <summary>
    /// property indicating whether the user wants to stay signed in after closing the app.
    /// </summary>
    [ObservableProperty]
    public partial bool RememberMe { get; set; } = true;

    /// <summary>
    /// property for the status message displayed to the user, which is observable and can be bound to the UI.
    /// </summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// property indicating whether there is an error state, which is observable and can be bound to the UI.
    /// </summary>
    [ObservableProperty]
    public partial bool IsError { get; set; }

    /// <summary>
    /// true when "Continue with Google" works (the API has its Google keys); the button is hidden otherwise.
    /// </summary>
    [ObservableProperty]
    public partial bool IsGoogleAvailable { get; set; }

    /// <summary>
    /// creates a new instance of the LoginViewModel class with the service that signs in through the API.
    /// </summary>
    /// <param name="auth">Signs in and out through the Phloem API.</param>
    /// <param name="cart">Holds the country picked on Home (used if Google creates a new account).</param>
    public LoginViewModel(AuthService auth, CartService cart)
    {
        _auth = auth;
        _cart = cart;
    }

    /// <summary>
    /// command that signs in through the API (asking for the two-step code when needed) and goes to Home.
    /// The button is disabled while it runs, so it can't be pressed twice.
    /// </summary>
    [RelayCommand]
    private async Task Login()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ShowError("Please enter your email and password.");
            return;
        }

        ShowInfo("Signing in…");
        var login = await _auth.LoginAsync(Email, Password, RememberMe);
        if (login.IsFailure)
        {
            ShowError(login.Error!.DisplayMessage);
            return;
        }

        // Two-step verification: ask for the code, then finish signing in
        if (login.Value!.RequiresTwoFactor)
        {
            var verified = await TwoFactorPrompt.CompleteAsync(_auth, login.Value.TwoFactorChallengeToken!, RememberMe);
            if (verified.IsFailure)
            {
                ShowError(verified.Error!.DisplayMessage);
                return;
            }
        }

        await FinishSignInAsync();
    }

    /// <summary>
    /// Checks whether "Continue with Google" works, so the button only shows when it will (called when the page
    /// appears). Stays hidden while the API has no Google keys, or can't be reached.
    /// </summary>
    public async Task LoadSignInOptionsAsync() => IsGoogleAvailable = await _auth.IsGoogleAvailableAsync();

    /// <summary>
    /// command for "Continue with Google": Google's page in the secure sign-in sheet, then the two-step code if the
    /// account has it on. The country (flag at the top) is only used if this creates a NEW account.
    /// </summary>
    [RelayCommand]
    private async Task ContinueWithGoogle()
    {
        ShowInfo("Opening Google…");
        var login = await _auth.SignInWithGoogleAsync(_cart.SelectedCountry.ApiCountry, RememberMe);
        if (login.IsFailure)
        {
            ShowError(login.Error!.DisplayMessage);
            return;
        }

        if (login.Value!.RequiresTwoFactor)
        {
            var verified = await TwoFactorPrompt.CompleteAsync(_auth, login.Value.TwoFactorChallengeToken!, RememberMe);
            if (verified.IsFailure)
            {
                ShowError(verified.Error!.DisplayMessage);
                return;
            }
        }

        await FinishSignInAsync();
    }

    /// <summary>Signed in (by password or Google): back to Checkout if that's where they were heading, otherwise
    /// close Log in and go Home.</summary>
    private async Task FinishSignInAsync()
    {
        Password = string.Empty;
        ShowInfo("Welcome back!");
        if (await AppNav.ResumeAfterLoginAsync()) return;
        await Shell.Current.Navigation.PopToRootAsync(false);
        await Shell.Current.GoToAsync("//home");
    }

    /// <summary>
    /// command that navigates the user to the registration page, allowing them to create a new account.
    /// </summary>
    [RelayCommand]
    private async Task GoToRegister() => await Shell.Current.GoToAsync("//account/register");

    /// <summary>
    /// command that asks the API to email a password-reset link to the email typed above.
    /// </summary>
    [RelayCommand]
    private async Task ForgotPassword()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            ShowError("Type your email address above first, then tap \"Forgot password?\".");
            return;
        }

        var sent = await _auth.ForgotPasswordAsync(Email);
        if (sent.IsFailure)
        {
            ShowError(sent.Error!.DisplayMessage);
            return;
        }

        // Same answer whether or not the account exists (nobody can use this to find accounts)
        ShowInfo("If an account exists for that email, a reset link has been sent.");
    }

    private void ShowError(string message)
    {
        IsError = true;
        StatusMessage = message;
    }

    private void ShowInfo(string message)
    {
        IsError = false;
        StatusMessage = message;
    }
}
