using Phloem.Services;
using Phloem.Services.Biometrics;

namespace Phloem.ViewModels;

/// <summary>
/// The "Sign in with Face ID / fingerprint next time?" pop-up after a login (password or Google). Shared like
/// <see cref="TwoFactorPrompt"/>, so it looks the same everywhere.
/// </summary>
/// <remarks>
/// Shown only when the phone has a sensor with a fingerprint / face set up, quick sign-in isn't already on for this
/// account, and the account hasn't said "Not now" before. "Not now" is remembered; it can still be switched on in
/// My account.
/// </remarks>
public static class BiometricOptInPrompt
{
    /// <summary>Offers quick sign-in to the account that just signed in, and switches it on if they agree.</summary>
    /// <param name="biometrics">Quick sign-in.</param>
    /// <param name="session">Who just signed in.</param>
    public static async Task OfferAsync(BiometricSignIn biometrics, SessionService session)
    {
        if (session.Profile is not { } profile || !biometrics.ShouldOffer(profile.Id)) return;

        var name = biometrics.DisplayName;
        var yes = await Shell.Current.DisplayAlertAsync(
            $"Sign in with {name}?",
            $"Next time, open Phloem with your {name} instead of typing your password. " +
            "The check happens on your phone: Phloem never receives your fingerprint or face.",
            $"Use {name}", "Not now");

        if (!yes)
        {
            biometrics.Decline(profile.Id);
            return;
        }

        var outcome = await biometrics.TurnOnAsync();
        if (outcome == BiometricOutcome.Failed)
            await Shell.Current.DisplayAlertAsync("Not switched on",
                $"We couldn't switch on {name} sign-in. You can try again in My account.", "OK");
    }
}
