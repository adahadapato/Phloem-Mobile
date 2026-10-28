using Phloem.Services.Api;
using Phloem.Services.Api.Contracts.Commands;
using Phloem.Services.Api.Contracts.Dtos;

namespace Phloem.Services;

/// <summary>
/// The signed-in user's own account: <c>/api/me</c>. Profile, personal details, photo, addresses, password and
/// notification switches.
/// </summary>
/// <remarks>
/// After a change, screens call <c>AuthService.RefreshProfileAsync</c> so <see cref="SessionService"/> shows the
/// server's copy.
/// </remarks>
public sealed class AccountService(ApiClient api)
{
    /// <summary>The form field the API reads the photo from (<c>PUT /api/me/photo</c>).</summary>
    private const string PhotoField = "photo";

    // ───────────────────────────── 1. Profile ─────────────────────────────

    /// <summary>The signed-in user's profile, addresses and settings.</summary>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The profile; or the API's error (e.g. 401 when not signed in).</returns>
    public Task<ApiResult<ProfileDto>> GetProfileAsync(CancellationToken ct = default) =>
        api.GetAsync<ProfileDto>("api/me", ct);

    /// <summary>Saves the name, country and phone number.</summary>
    /// <summary>Accepts the Terms version shown (409 "Terms.Outdated" if a newer one was published meanwhile).</summary>
    public Task<ApiResult> AcceptTermsAsync(int version, CancellationToken ct = default) =>
        api.PostAsync("api/me/terms/accept", new { version }, ct);

    /// <summary>"Got it" on the updated Privacy Policy notice.</summary>
    public Task<ApiResult> AcknowledgePrivacyAsync(int version, CancellationToken ct = default) =>
        api.PostAsync("api/me/privacy/seen", new { version }, ct);

    public Task<ApiResult> UpdateProfileAsync(UpdateProfileCommand command, CancellationToken ct = default) =>
        api.PutAsync("api/me", command, ct);

    /// <summary>Saves the date of birth and home address (needed to open a shop or send a parcel).</summary>
    public Task<ApiResult> UpdatePersonalDetailsAsync(UpdatePersonalDetailsCommand command,
                                                      CancellationToken ct = default) =>
        api.PutAsync("api/me/personal-details", command, ct);

    // ───────────────────────────── 2. Photo ─────────────────────────────

    /// <summary>Uploads a new profile photo (JPEG, PNG, WebP or HEIC).</summary>
    /// <returns>The photo's address on the server; or the API's error (e.g. too large).</returns>
    public Task<ApiResult<string>> ChangePhotoAsync(Stream photo, string fileName, string contentType,
                                                    CancellationToken ct = default) =>
        api.UploadAsync<string>(HttpMethod.Put, "api/me/photo", PhotoField, photo, fileName, contentType, ct: ct);

    /// <summary>Removes the profile photo.</summary>
    public Task<ApiResult> RemovePhotoAsync(CancellationToken ct = default) =>
        api.DeleteAsync("api/me/photo", ct);

    // ───────────────────────────── 3. Addresses ─────────────────────────────

    /// <summary>Saves a delivery address to the account.</summary>
    /// <param name="command">Label, full address, country and whether it becomes the default.</param>
    /// <param name="ct">Cancellation.</param>
    /// <returns>The new address's Id; or the API's error (e.g. a missing field).</returns>
    public Task<ApiResult<Guid>> AddAddressAsync(AddAddressCommand command, CancellationToken ct = default) =>
        api.PostAsync<Guid>("api/me/addresses", command, ct);

    // ───────────────────────────── 4. Password ─────────────────────────────

    /// <summary>Changes the password. The API then signs the user out on every device.</summary>
    public Task<ApiResult> ChangePasswordAsync(ChangePasswordCommand command, CancellationToken ct = default) =>
        api.PutAsync("api/me/password", command, ct);

    // ───────────────────────────── 5. Notifications ─────────────────────────────

    /// <summary>Saves every notification switch at once.</summary>
    public Task<ApiResult> UpdateNotificationsAsync(UpdateNotificationsCommand command,
                                                    CancellationToken ct = default) =>
        api.PutAsync("api/me/notifications", command, ct);
}
