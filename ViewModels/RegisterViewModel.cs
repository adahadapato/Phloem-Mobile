using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Services;
using Phloem.Services.Api.Contracts;

namespace Phloem.ViewModels;

/// <summary>
/// contains the properties and commands for the RegisterPage view.
/// </summary>
/// <remarks>
/// <para>Creates the account through the Phloem API (<see cref="AuthService"/>) and signs straight in.</para>
/// <para>The home address is optional at sign-up, but is needed later to open a shop or send a parcel; when given,
/// it also becomes the "Home" delivery address. The country is the one picked on Home (flag at the top).</para>
/// </remarks>
public partial class RegisterViewModel : ObservableObject
{
    private readonly AuthService _auth;
    private readonly CartService _cart;

    /// <summary>
    /// property for the user's first name.
    /// </summary>
    [ObservableProperty]
    public partial string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// property for the user's last name (surname).
    /// </summary>
    [ObservableProperty]
    public partial string LastName { get; set; } = string.Empty;

    /// <summary>
    /// property for the user's email address.
    /// </summary>
    [ObservableProperty]
    public partial string Email { get; set; } = string.Empty;

    /// <summary>
    /// property for the user's phone number (optional).
    /// </summary>
    [ObservableProperty]
    public partial string PhoneNumber { get; set; } = string.Empty;

    /// <summary>
    /// home address, first line (optional; needed later to sell or send parcels).
    /// </summary>
    [ObservableProperty]
    public partial string AddressLine1 { get; set; } = string.Empty;

    /// <summary>
    /// home address, town or city.
    /// </summary>
    [ObservableProperty]
    public partial string City { get; set; } = string.Empty;

    /// <summary>
    /// home address, region / state (Ghana, Nigeria) or postcode (UK).
    /// </summary>
    [ObservableProperty]
    public partial string RegionOrPostcode { get; set; } = string.Empty;

    /// <summary>
    /// property for the user's password.
    /// </summary>
    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    /// <summary>
    /// property indicating whether the user has agreed to the terms and conditions.
    /// </summary>
    [ObservableProperty]
    public partial bool AgreedToTerms { get; set; }

    /// <summary>
    /// true when the person chose "I want to sell" on the Buyer | Seller toggle.
    /// After sign-up they are taken straight to seller setup instead of Home.
    /// </summary>
    [ObservableProperty]
    public partial bool IsSeller { get; set; }

    /// <summary>
    /// property for the status message to display to the user.
    /// </summary>
    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    /// <summary>
    /// property indicating whether there is an error in the registration process.
    /// </summary>
    [ObservableProperty]
    public partial bool IsError { get; set; }

    /// <summary>
    /// true when "Continue with Google" works (the API has its Google keys); the button is hidden otherwise.
    /// </summary>
    [ObservableProperty]
    public partial bool IsGoogleAvailable { get; set; }

    /// <summary>
    /// creates a new instance of the RegisterViewModel class.
    /// </summary>
    /// <param name="auth">Creates the account through the Phloem API.</param>
    /// <param name="cart">Holds the country picked on Home.</param>
    public RegisterViewModel(AuthService auth, CartService cart)
    {
        _auth = auth;
        _cart = cart;
    }

    /// <summary>
    /// command to handle the sign-up process when the user clicks the sign-up button.
    /// Checks the form, creates the account through the API, and signs the user in automatically.
    /// Sellers then continue to seller setup; buyers go to Home. The API checks everything again.
    /// </summary>
    [RelayCommand]
    private async Task SignUp()
    {
        if (string.IsNullOrWhiteSpace(FirstName) || string.IsNullOrWhiteSpace(LastName) ||
            string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            ShowError("Please fill in your first name, last name, email and password.");
            return;
        }

        if (!AgreedToTerms)
        {
            ShowError("Please accept the Terms & Conditions.");
            return;
        }

        // The home address is all or nothing: a half address can't be used to open a shop or send a parcel
        var anyAddress = !string.IsNullOrWhiteSpace(AddressLine1) || !string.IsNullOrWhiteSpace(City) ||
                         !string.IsNullOrWhiteSpace(RegionOrPostcode);
        var fullAddress = !string.IsNullOrWhiteSpace(AddressLine1) && !string.IsNullOrWhiteSpace(City) &&
                          !string.IsNullOrWhiteSpace(RegionOrPostcode);
        if (anyAddress && !fullAddress)
        {
            ShowError("Please complete your home address (street, town or city, and region or postcode), " +
                      "or leave it empty and add it later.");
            return;
        }

        var country = _cart.SelectedCountry.ApiCountry;
        var command = new RegisterCommand(
            FirstName.Trim(),
            LastName.Trim(),
            Email.Trim(),
            string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim(),
            country,
            DateOfBirth: null,
            fullAddress ? new HomeAddressInput(AddressLine1.Trim(), null, City.Trim(), RegionOrPostcode.Trim(), country)
                        : null,
            Password,
            AgreedToTerms);

        IsError = false;
        StatusMessage = "Creating your account…";

        var registered = await _auth.RegisterAsync(command);
        if (registered.IsFailure)
        {
            ShowError(registered.Error!.DisplayMessage);
            return;
        }

        IsError = false;
        StatusMessage = IsSeller
            ? $"Welcome, {FirstName.Trim()}! Now let's set up your shop."
            : $"Welcome, {FirstName.Trim()}! Your account is ready.";
        Password = string.Empty;

        // Close Sign up (it sits on top of My Account), then sellers continue to shop setup; buyers go Home
        await Shell.Current.Navigation.PopToRootAsync(false);
        await Shell.Current.GoToAsync(IsSeller ? "//home/sellersetup" : "//home");
    }

    /// <summary>
    /// Checks whether "Continue with Google" works, so the button only shows when it will (called when the page
    /// appears).
    /// </summary>
    public async Task LoadSignInOptionsAsync() => IsGoogleAvailable = await _auth.IsGoogleAvailableAsync();

    /// <summary>
    /// command for "Continue with Google": no form needed. Google gives the name and email; the country is the one
    /// picked on Home. If the email already has an account, the person is simply signed in to it (with the two-step
    /// code if they have it on). Then the same as "Create account": sellers go to shop setup, buyers Home.
    /// </summary>
    /// <remarks>Agreeing to the Terms is shown under the button ("By continuing with Google you agree…"), as Google
    /// sign-up pages usually do.</remarks>
    [RelayCommand]
    private async Task ContinueWithGoogle()
    {
        IsError = false;
        StatusMessage = "Opening Google…";

        var signedIn = await _auth.SignInWithGoogleAsync(_cart.SelectedCountry.ApiCountry, remember: true);
        if (signedIn.IsFailure)
        {
            ShowError(signedIn.Error!.DisplayMessage);
            return;
        }

        if (signedIn.Value!.RequiresTwoFactor)
        {
            var verified = await TwoFactorPrompt.CompleteAsync(_auth, signedIn.Value.TwoFactorChallengeToken!,
                                                               remember: true);
            if (verified.IsFailure)
            {
                ShowError(verified.Error!.DisplayMessage);
                return;
            }
        }

        IsError = false;
        StatusMessage = IsSeller ? "Welcome! Now let's set up your shop." : "Welcome to Phloem!";

        await Shell.Current.Navigation.PopToRootAsync(false);
        await Shell.Current.GoToAsync(IsSeller ? "//home/sellersetup" : "//home");
    }

    private void ShowError(string message)
    {
        IsError = true;
        StatusMessage = message;
    }
}
