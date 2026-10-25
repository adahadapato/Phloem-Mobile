using System.Windows.Input;
using Phloem.Services;
using Phloem.Views;

namespace Phloem;

/// <summary>
/// App-wide navigation helpers that any page can use straight from XAML, e.g.
///   &lt;TapGestureRecognizer Command="{x:Static phloem:AppNav.GoHomeCommand}" /&gt;
/// </summary>
public static class AppNav
{
    /// <summary>Phloem logo tapped: go to the Home tab and close anything opened on top of it.</summary>
    public static ICommand GoHomeCommand { get; } = new Command(async () => await GoHomeAsync());

    public static async Task GoHomeAsync()
    {
        if (Shell.Current is null) return;

        Shell.Current.FlyoutIsPresented = false;
        await Shell.Current.GoToAsync("//home");

        // If pages were opened on top of Home (Vendors, Seller setup…), go back to Home itself
        if (Shell.Current.Navigation.NavigationStack.Count > 1)
            await Shell.Current.Navigation.PopToRootAsync(false);
    }

    /// <summary>True while someone who tapped Checkout signed out is on their way to Log in.</summary>
    private static bool _checkoutAfterLogin;

    /// <summary>
    /// Every "Checkout" button comes here: Checkout needs a signed-in user. Signed out → Log in first, and straight
    /// back to Checkout afterwards (see <see cref="ResumeAfterLoginAsync"/>). The basket stays as it is.
    /// </summary>
    public static async Task GoToCheckoutAsync()
    {
        if (Shell.Current is null) return;

        var session = IPlatformApplication.Current?.Services.GetService<SessionService>();
        if (session?.IsLoggedIn == true)
        {
            await Shell.Current.GoToAsync(nameof(CheckoutPage));
            return;
        }

        await Shell.Current.DisplayAlertAsync("Log in to check out",
            "Please log in (or sign up) to check out. Your basket will still be here.", "Log in");
        _checkoutAfterLogin = true;
        await Shell.Current.GoToAsync("//account/login");
    }

    /// <summary>
    /// Called by Log in after a successful sign-in: goes to Checkout if that's where the user was heading.
    /// </summary>
    /// <returns>True when it went to Checkout; false when Log in should go where it normally goes.</returns>
    public static async Task<bool> ResumeAfterLoginAsync()
    {
        if (!_checkoutAfterLogin || Shell.Current is null) return false;
        _checkoutAfterLogin = false;

        await Shell.Current.Navigation.PopToRootAsync(false);   // close Log in
        await Shell.Current.GoToAsync($"//orders/{nameof(CheckoutPage)}");
        return true;
    }

    // ── Terms, Privacy and Help (the text comes from the API; see LegalPage) ──

    /// <summary>Terms &amp; Conditions.</summary>
    public static ICommand OpenTermsCommand { get; } = new Command(async () => await OpenLegalAsync("Terms"));

    /// <summary>Help &amp; support.</summary>
    public static ICommand OpenHelpCommand { get; } = new Command(async () => await OpenLegalAsync("Help"));

    /// <summary>Privacy Policy.</summary>
    public static ICommand OpenPrivacyCommand { get; } = new Command(async () => await OpenLegalAsync("Privacy"));

    /// <summary>Terms, at "Selling on Phloem".</summary>
    public static ICommand OpenSellerTermsCommand { get; } = new Command(async () => await OpenLegalAsync("Terms", "sellers"));

    /// <summary>Terms, at "Sending parcels".</summary>
    public static ICommand OpenParcelTermsCommand { get; } = new Command(async () => await OpenLegalAsync("Terms", "parcels"));

    /// <summary>Terms, at "Buying food".</summary>
    public static ICommand OpenBuyerTermsCommand { get; } = new Command(async () => await OpenLegalAsync("Terms", "buyers"));

    /// <summary>Opens the Terms or the Privacy Policy, optionally at a section.</summary>
    /// <param name="type">"Terms", "Privacy" or "Help".</param>
    /// <param name="section">An anchor in the text ("sellers", "parcels"…), or null for the top.</param>
    public static async Task OpenLegalAsync(string type, string? section = null)
    {
        if (Shell.Current is null) return;
        Shell.Current.FlyoutIsPresented = false;
        var route = $"legal?type={type}";
        if (!string.IsNullOrWhiteSpace(section)) route += $"&section={Uri.EscapeDataString(section)}";
        await Shell.Current.GoToAsync(route);
    }
}
