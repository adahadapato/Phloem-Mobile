using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Maui.Authentication;
using Phloem.Services.Api;
using Phloem.Settings;
using Phloem.Services.Api.Contracts;
using Phloem.Services.Api.Contracts.Commands;
using Phloem.Services.Api.Contracts.Dtos;

namespace Phloem.Services;

/// <summary>
/// Signing in and out: <c>/api/auth</c>. After a successful sign-in it keeps the tokens (<see cref="TokenStore"/>),
/// loads the profile (<see cref="AccountService"/>) and fills <see cref="SessionService"/>, so every screen knows
/// who is signed in.
/// </summary>
public sealed class AuthService(
    ApiClient api,
    TokenStore tokens,
    AccountService account,
    SessionService session,
    IOptions<ApiSettings> apiSettings,
    ILogger<AuthService> logger)
{
    /// <summary>No field problems (errors made here, not by the API).</summary>
    private static readonly IReadOnlyDictionary<string, string[]> NoFieldErrors = new Dictionary<string, string[]>();

    // ───────────────────────────── 1. Sign in ─────────────────────────────

    /// <summary>Email + password.</summary>
    /// <param name="email">The account's email.</param>
    /// <param name="password">The password.</param>
    /// <param name="remember">Stay signed in after the app closes.</param>
    /// <returns>
    /// The API's answer. When <see cref="AuthTokensDto.RequiresTwoFactor"/> is true the user is NOT signed in yet:
    /// ask for the code and call <see cref="CompleteTwoFactorAsync"/>.
    /// </returns>
    public async Task<ApiResult<AuthTokensDto>> LoginAsync(string email, string password, bool remember)
    {
        var login = await api.PostAsync<AuthTokensDto>("api/auth/login", new LoginCommand(email.Trim(), password));
        if (login.IsFailure || login.Value!.RequiresTwoFactor) return login;

        var signedIn = await SignInAsync(login.Value, remember);
        return signedIn.IsSuccess ? login : signedIn.Error!;
    }

    /// <summary>Second step of a two-factor sign-in.</summary>
    /// <param name="challengeToken">From the login answer.</param>
    /// <param name="code">The 6-digit code, or a recovery code.</param>
    /// <param name="isRecoveryCode">True when a recovery code was typed.</param>
    /// <param name="remember">Stay signed in after the app closes.</param>
    public async Task<ApiResult> CompleteTwoFactorAsync(string challengeToken, string code, bool isRecoveryCode,
                                                        bool remember)
    {
        var result = await api.PostAsync<AuthTokensDto>("api/auth/login/two-factor",
            new CompleteTwoFactorLoginCommand(challengeToken, code.Trim(), isRecoveryCode));
        return result.IsFailure ? result.Error! : await SignInAsync(result.Value!, remember);
    }

    /// <summary>Creates the account and signs straight in.</summary>
    /// <param name="command">The sign-up form.</param>
    public async Task<ApiResult> RegisterAsync(RegisterCommand command)
    {
        var result = await api.PostAsync<AuthTokensDto>("api/auth/register", command);
        return result.IsFailure ? result.Error! : await SignInAsync(result.Value!, remember: true);
    }

    // ───────────────────────────── 1b. Continue with Google ─────────────────────────────

    /// <summary>Asks the API whether "Continue with Google" is switched on (its Google keys are set).</summary>
    /// <param name="ct">Cancellation.</param>
    /// <returns>True when the button should show; false when Google is off or the API can't be reached.</returns>
    public async Task<bool> IsGoogleAvailableAsync(CancellationToken ct = default)
    {
        var providers = await api.GetAsync<ExternalProvidersDto>("api/auth/external/providers", ct);
        return providers.IsSuccess && providers.Value!.Google;
    }

    /// <summary>
    /// "Continue with Google": opens Google's page in the phone's secure sign-in sheet, then swaps the one-time code
    /// the API sends back for a session.
    /// </summary>
    /// <param name="country">The country picked in the app (flag at the top); used only if a NEW account is
    /// created.</param>
    /// <param name="remember">Stay signed in after the app closes.</param>
    /// <returns>
    /// The API's answer, exactly like <see cref="LoginAsync"/>: when <see cref="AuthTokensDto.RequiresTwoFactor"/> is
    /// true, ask for the code and call <see cref="CompleteTwoFactorAsync"/>. A closed sheet gives the error code
    /// <see cref="ExternalSignIn.CancelledCode"/>.
    /// </returns>
    /// <remarks>Google answers the API, never the app: the app only ever sees the one-time code, then the API's own
    /// tokens, the same as after a password login.</remarks>
    public async Task<ApiResult<AuthTokensDto>> SignInWithGoogleAsync(Country country, bool remember)
    {
        // 1. Google's page, through the API (which keeps Google's secret)
        var start = new Uri(apiSettings.Value.ResolveBaseAddress(), $"api/auth/external/google?country={country}");

        WebAuthenticatorResult sheet;
        try
        {
            sheet = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions
            {
                Url = start,
                CallbackUrl = ExternalSignIn.CallbackUri
            });
        }
        catch (OperationCanceledException)   // the person closed the sheet (TaskCanceledException included)
        {
            return new ApiError(0, ExternalSignIn.CancelledCode, "Sign-in cancelled.", NoFieldErrors);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "The Google sign-in sheet couldn't be opened.");
            return ApiError.Unexpected(0);
        }

        // 2. The API said no (e.g. Google hasn't confirmed the email): its code and message come in the link
        if (sheet.Properties.TryGetValue(ExternalSignIn.ErrorKey, out var errorCode))
        {
            var message = sheet.Properties.GetValueOrDefault(ExternalSignIn.MessageKey);
            return new ApiError(0, errorCode,
                                string.IsNullOrWhiteSpace(message) ? "Signing in with Google didn't finish. Please try again." : message,
                                NoFieldErrors);
        }

        if (!sheet.Properties.TryGetValue(ExternalSignIn.CodeKey, out var code) || string.IsNullOrWhiteSpace(code))
            return ApiError.Unexpected(0);

        // 3. Swap the one-time code for the API's own tokens (suspension and 2FA are checked here)
        var exchanged = await api.PostAsync<AuthTokensDto>("api/auth/external/exchange",
                                                           new ExchangeLoginCodeCommand(code));
        if (exchanged.IsFailure || exchanged.Value!.RequiresTwoFactor) return exchanged;

        var signedIn = await SignInAsync(exchanged.Value, remember);
        return signedIn.IsSuccess ? exchanged : signedIn.Error!;
    }

    // ───────────────────────────── 2. Session ─────────────────────────────

    /// <summary>
    /// App start: if the user ticked "Remember me" last time, renews the sign-in and loads the profile.
    /// </summary>
    /// <returns>True when the user is signed in again.</returns>
    public async Task<bool> RestoreSessionAsync()
    {
        if (!await tokens.LoadAsync()) return false;

        // The token handler renews the tokens on the way if they ran out
        var profile = await account.GetProfileAsync();
        if (profile.IsSuccess)
        {
            session.SignIn(profile.Value!, tokens.Roles);
            return true;
        }

        // Rejected = the sign-in is over; no connection = keep the tokens and try again next start
        if (profile.Error!.IsUnauthorized) await tokens.ClearAsync();
        logger.LogInformation("Couldn't restore the sign-in: {Code}.", profile.Error.Code);
        return false;
    }

    /// <summary>Reloads the profile (e.g. after opening a shop or changing details).</summary>
    public async Task<ApiResult> RefreshProfileAsync()
    {
        var profile = await account.GetProfileAsync();
        if (profile.IsFailure) return profile.Error!;

        session.SignIn(profile.Value!, tokens.Roles);
        return ApiResult.Success();
    }

    /// <summary>Signs out on this device and cancels the refresh token on the server.</summary>
    public async Task LogoutAsync()
    {
        if (tokens.RefreshToken is { } refreshToken)
        {
            // Best effort: the user is signed out here even if the server can't be reached
            var revoked = await api.PostAsync("api/auth/logout", new RevokeTokenCommand(refreshToken));
            if (revoked.IsFailure) logger.LogInformation("Logout not confirmed by the API: {Code}.", revoked.Error!.Code);
        }

        await tokens.ClearAsync();
        session.SignOut();
    }

    // ───────────────────────────── 3. Password ─────────────────────────────

    /// <summary>Emails a reset link (the API answers the same whether or not the account exists).</summary>
    /// <param name="email">The account's email.</param>
    public Task<ApiResult> ForgotPasswordAsync(string email) =>
        api.PostAsync("api/auth/forgot-password", new ForgotPasswordCommand(email.Trim()));

    // ───────────────────────────── Helpers ─────────────────────────────

    /// <summary>Keeps the tokens, then loads the profile into the session.</summary>
    private async Task<ApiResult> SignInAsync(AuthTokensDto tokensDto, bool remember)
    {
        await tokens.SaveAsync(tokensDto, remember);

        var profile = await account.GetProfileAsync();
        if (profile.IsFailure)
        {
            await tokens.ClearAsync();
            return profile.Error!;
        }

        session.SignIn(profile.Value!, tokens.Roles);
        return ApiResult.Success();
    }
}
