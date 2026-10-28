namespace Phloem.Services;

/// <summary>
/// The app's side of Terms acceptance (the API decides; the website does the same):
/// <list type="bullet">
///   <item><description>After sign-in / when Home appears: "We've updated our Terms" if
///   <c>Profile.Terms.NeedsToAcceptTerms</c>, once per app run ("Not now" lets people keep browsing).</description></item>
///   <item><description>A newer Privacy Policy: a one-time notice ("Got it").</description></item>
///   <item><description>An action the API refused with <c>Terms.NotAccepted</c> (checkout, adding a dish) opens the
///   same screen through <see cref="AppNav.OpenTermsUpdateAsync"/>.</description></item>
/// </list>
/// </summary>
public sealed class TermsPromptService(SessionService session, AccountService account, AuthService auth)
{
    /// <summary>The Terms version someone said "Not now" to during this run of the app.</summary>
    public int DismissedVersion { get; set; }

    private int _privacyShownVersion;
    private bool _checking;

    /// <summary>Shows what's needed, if anything. Safe to call every time Home appears.</summary>
    public async Task CheckAsync()
    {
        if (_checking || session.Profile?.Terms is not { } terms || Shell.Current is null) return;
        _checking = true;
        try
        {
            if (terms.NeedsToAcceptTerms && DismissedVersion < terms.CurrentTermsVersion)
            {
                await AppNav.OpenTermsUpdateAsync();
                return;
            }

            if (terms.NeedsToSeePrivacy && _privacyShownVersion < terms.CurrentPrivacyVersion)
            {
                _privacyShownVersion = terms.CurrentPrivacyVersion;
                var gotIt = await Shell.Current.DisplayAlertAsync("We've updated our Privacy Policy",
                    "It explains how we keep and use your details. You can read it now or any time from Help & support.",
                    "Got it", "Read it");
                await account.AcknowledgePrivacyAsync(terms.CurrentPrivacyVersion);   // informed either way
                await auth.RefreshProfileAsync();
                if (!gotIt) await AppNav.OpenLegalAsync("Privacy");
            }
        }
        catch (Exception)
        {
            // Never stop the app over this: it's asked again next time
        }
        finally
        {
            _checking = false;
        }
    }
}
