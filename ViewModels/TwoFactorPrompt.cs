using Phloem.Services;
using Phloem.Services.Api;

namespace Phloem.ViewModels;

/// <summary>
/// The "Two-step verification" pop-up: asks for the 6-digit code (or a recovery code) and finishes the sign-in.
/// Shared by Log in and Sign up, for password AND Google sign-in, so it looks and works the same everywhere.
/// </summary>
public static class TwoFactorPrompt
{
    /// <summary>Asks for the code, then finishes signing in.</summary>
    /// <param name="auth">Finishes the sign-in.</param>
    /// <param name="challengeToken">From the API's answer (<see cref="AuthTokensDto.TwoFactorChallengeToken"/>).</param>
    /// <param name="remember">Stay signed in after the app closes.</param>
    /// <returns>Success when signed in; an error when the code was wrong; a "cancelled" error when the pop-up was
    /// closed.</returns>
    public static async Task<ApiResult> CompleteAsync(AuthService auth, string challengeToken, bool remember)
    {
        var code = await Shell.Current.DisplayPromptAsync(
            "Two-step verification",
            "Enter the 6-digit code from your authenticator app, or one of your recovery codes.",
            accept: "Verify", cancel: "Cancel", placeholder: "123456", maxLength: 20);

        if (string.IsNullOrWhiteSpace(code))
            return new ApiError(0, ExternalSignIn.CancelledCode, "Sign-in cancelled.",
                                new Dictionary<string, string[]>());

        // A 6-digit number is an authenticator code; anything else is a recovery code
        var trimmed = code.Trim();
        var isRecoveryCode = !(trimmed.Length == 6 && trimmed.All(char.IsDigit));

        return await auth.CompleteTwoFactorAsync(challengeToken, trimmed, isRecoveryCode, remember);
    }
}
