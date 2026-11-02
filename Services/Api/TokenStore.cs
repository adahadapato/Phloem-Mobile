using Phloem.Services.Api.Contracts.Dtos;

namespace Phloem.Services.Api;

/// <summary>
/// Keeps the sign-in tokens: in memory always, and in the device's secure storage when "Remember me" is ticked, so
/// the user stays signed in after closing the app.
/// </summary>
/// <remarks>
/// <para>The access token lasts 15 minutes; <see cref="AuthTokenHandler"/> swaps the refresh token for new ones when it
/// runs out. If that fails, <see cref="SessionExpired"/> is raised and the user must sign in again.</para>
/// <para>Registered as a SINGLETON: every API call shares the same tokens and the same <see cref="RefreshLock"/>.</para>
/// </remarks>
public sealed class TokenStore
{
    private const string AccessTokenKey = "phloem.access_token";
    private const string AccessTokenExpiresKey = "phloem.access_token_expires";
    private const string RefreshTokenKey = "phloem.refresh_token";
    private const string RolesKey = "phloem.roles";
    private const string PermissionsKey = "phloem.permissions";
    private const string CountriesKey = "phloem.countries";

    /// <summary>Refresh this long before the access token runs out, so a request never leaves with a dying token.</summary>
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(30);

    /// <summary>The current access token (sent as "Bearer …"); null when signed out.</summary>
    public string? AccessToken { get; private set; }

    /// <summary>When <see cref="AccessToken"/> stops working (UTC).</summary>
    public DateTime? AccessTokenExpiresAt { get; private set; }

    /// <summary>Swapped for new tokens when the access token runs out; null when signed out.</summary>
    public string? RefreshToken { get; private set; }

    /// <summary>The user's roles from the last sign-in or refresh (e.g. "Customer", "Seller", "Admin").</summary>
    public IReadOnlyCollection<string> Roles { get; private set; } = [];

    /// <summary>
    /// Staff permissions the backend sent with the last sign-in or renewal ("*" = every permission, the owners);
    /// empty for everyone else. Only used to show or hide menus: the API checks every call itself.
    /// </summary>
    public IReadOnlyCollection<string> Permissions { get; private set; } = [];

    /// <summary>The countries the backend says this staff member covers ("*" = every country).</summary>
    public IReadOnlyCollection<string> Countries { get; private set; } = [];

    /// <summary>True when the tokens are also kept on the device ("Remember me").</summary>
    public bool Remember { get; private set; }

    /// <summary>True when there is a refresh token to renew the sign-in with.</summary>
    public bool CanRefresh => !string.IsNullOrEmpty(RefreshToken);

    /// <summary>True when the access token is missing or about to run out.</summary>
    public bool AccessTokenExpiresSoon =>
        AccessToken is null || AccessTokenExpiresAt is null ||
        AccessTokenExpiresAt.Value.ToUniversalTime() - DateTime.UtcNow < RefreshMargin;

    /// <summary>Only one token refresh at a time (several requests may find the token expired together).</summary>
    public SemaphoreSlim RefreshLock { get; } = new(1, 1);

    /// <summary>Raised when the sign-in could not be renewed: the user has been signed out.</summary>
    public event EventHandler? SessionExpired;

    /// <summary>Keeps new tokens (after login, register, two-factor or refresh).</summary>
    /// <param name="tokens">The API's answer; must contain tokens.</param>
    /// <param name="remember">Also keep them on the device.</param>
    /// <exception cref="ArgumentException">The answer has no tokens (e.g. a two-factor challenge).</exception>
    public async Task SaveAsync(AuthTokensDto tokens, bool remember)
    {
        if (string.IsNullOrEmpty(tokens.AccessToken) || string.IsNullOrEmpty(tokens.RefreshToken))
            throw new ArgumentException("The sign-in answer has no tokens.", nameof(tokens));

        AccessToken = tokens.AccessToken;
        AccessTokenExpiresAt = tokens.ExpiresAt?.ToUniversalTime();
        RefreshToken = tokens.RefreshToken;
        Roles = tokens.Roles ?? [];
        Permissions = tokens.Permissions ?? [];
        Countries = tokens.Countries ?? [];
        Remember = remember;

        if (!remember)
        {
            await RemoveFromDeviceAsync();
            return;
        }

        await WriteAsync(AccessTokenKey, AccessToken);
        await WriteAsync(AccessTokenExpiresKey, AccessTokenExpiresAt?.ToString("O"));
        await WriteAsync(RefreshTokenKey, RefreshToken);
        await WriteAsync(RolesKey, string.Join(',', Roles));
        await WriteAsync(PermissionsKey, string.Join(',', Permissions));
        await WriteAsync(CountriesKey, string.Join(',', Countries));
    }

    /// <summary>Reads tokens kept on the device (app start).</summary>
    /// <returns>True when a refresh token was found, so the sign-in can be restored.</returns>
    public async Task<bool> LoadAsync()
    {
        var refreshToken = await ReadAsync(RefreshTokenKey);
        if (string.IsNullOrEmpty(refreshToken)) return false;

        RefreshToken = refreshToken;
        AccessToken = await ReadAsync(AccessTokenKey);
        AccessTokenExpiresAt = DateTime.TryParse(await ReadAsync(AccessTokenExpiresKey), null,
                                                 System.Globalization.DateTimeStyles.RoundtripKind, out var expires)
            ? expires.ToUniversalTime()
            : null;
        Roles = (await ReadAsync(RolesKey))?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];
        Permissions = (await ReadAsync(PermissionsKey))?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];
        Countries = (await ReadAsync(CountriesKey))?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [];
        Remember = true;
        return true;
    }

    /// <summary>Forgets every token (sign-out).</summary>
    public async Task ClearAsync()
    {
        AccessToken = null;
        AccessTokenExpiresAt = null;
        RefreshToken = null;
        Roles = [];
        Permissions = [];
        Countries = [];
        Remember = false;
        await RemoveFromDeviceAsync();
    }

    /// <summary>The sign-in couldn't be renewed: forget the tokens and tell the app.</summary>
    public async Task ExpireAsync()
    {
        await ClearAsync();
        SessionExpired?.Invoke(this, EventArgs.Empty);
    }

    // ───────────────────────────── Device storage ─────────────────────────────
    // Secure storage can fail on some devices / unpackaged Windows builds: then the tokens simply live in memory only.

    private static async Task WriteAsync(string key, string? value)
    {
        try
        {
            if (value is null) SecureStorage.Default.Remove(key);
            else await SecureStorage.Default.SetAsync(key, value);
        }
        catch (Exception)
        {
            // Not available on this device: memory only.
        }
    }

    private static async Task<string?> ReadAsync(string key)
    {
        try { return await SecureStorage.Default.GetAsync(key); }
        catch (Exception) { return null; }
    }

    private static Task RemoveFromDeviceAsync()
    {
        try
        {
            SecureStorage.Default.Remove(AccessTokenKey);
            SecureStorage.Default.Remove(AccessTokenExpiresKey);
            SecureStorage.Default.Remove(RefreshTokenKey);
            SecureStorage.Default.Remove(RolesKey);
            SecureStorage.Default.Remove(PermissionsKey);
            SecureStorage.Default.Remove(CountriesKey);
        }
        catch (Exception)
        {
            // Not available on this device: nothing was stored.
        }
        return Task.CompletedTask;
    }
}
