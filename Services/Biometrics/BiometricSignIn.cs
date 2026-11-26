using Phloem.Services.Api;

namespace Phloem.Services.Biometrics;

/// <summary>
/// Fingerprint / Face ID sign-in: a scan unlocks the sign-in already kept on this phone. Used by
/// <see cref="AuthService"/> (start-up and the Log in button), the offer after login and the My account switch.
/// </summary>
/// <remarks>
/// <para><b>How it works.</b> After a normal sign-in (password or Google, plus two-step if it's on) the tokens are kept
/// in the phone's secure storage, exactly like "Remember me". With this switched on, the app asks for a scan BEFORE
/// it loads them, at start-up and from the Log in page. No new kind of sign-in exists on the API: the scan only
/// unlocks the session the person already has, and the API's own rules still apply (the refresh token runs out
/// after 30 days of not being used; a password change or reset ends every session).</para>
/// <para><b>Log out only locks</b> while this is on: the sign-in stays on the phone, and the next login starts with
/// a scan (the Log in page asks at once; the password is the fallback).</para>
/// <para><b>Switched off</b> when the sign-in can't be renewed, after a password change, when someone else signs in
/// on this phone, from the My account switch, and when the phone's fingerprints / faces change (then the saved
/// sign-in is also deleted, so the password is needed).</para>
/// <para>Phloem never receives fingerprint or face data.</para>
/// </remarks>
public sealed class BiometricSignIn
{
    private readonly IBiometricService _biometrics;
    private readonly BiometricSettings _settings;
    private readonly TokenStore _tokens;
    private readonly SessionService _session;

    /// <summary>Shown when the phone's fingerprints / faces changed and the saved sign-in had to be deleted.</summary>
    public const string ChangedMessage =
        "Your phone's fingerprint or face settings changed, so please sign in with your password. " +
        "You can switch quick sign-in back on afterwards.";

    /// <summary>Switches itself off when the sign-in can't be renewed (nothing left to unlock).</summary>
    public BiometricSignIn(IBiometricService biometrics, BiometricSettings settings, TokenStore tokens,
                           SessionService session)
    {
        _biometrics = biometrics;
        _settings = settings;
        _tokens = tokens;
        _session = session;
        tokens.KeepStrictOnDevice = settings.IsOn;   // seller / staff sign-ins may stay on the phone only behind the scan
        tokens.SessionExpired += (_, _) => TurnOff();
    }

    /// <summary>What the phone can do right now.</summary>
    public BiometricStatus Status => _biometrics.GetStatus();

    /// <summary>"Face ID", "Touch ID", "fingerprint" or "fingerprint or face".</summary>
    public string DisplayName => _biometrics.DisplayName;

    /// <summary>True when the app can open the phone's "add a fingerprint / face" screen.</summary>
    public bool CanOpenSetup => _biometrics.CanOpenSetup;

    /// <summary>Opens the phone's "add a fingerprint / face" screen.</summary>
    public void OpenSetup() => _biometrics.OpenSetup();

    /// <summary>True when switched on (for whoever switched it on).</summary>
    public bool IsOn => _settings.IsOn;

    /// <summary>True when switched on for this account.</summary>
    /// <param name="userId">An account Id.</param>
    public bool IsOnFor(Guid? userId) => userId is not null && _settings.OnForUserId == userId;

    /// <summary>
    /// A message waiting for the Log in page (e.g. <see cref="ChangedMessage"/>); read it with
    /// <see cref="TakeNotice"/>.
    /// </summary>
    public string? PendingNotice { get; private set; }

    /// <summary>Returns <see cref="PendingNotice"/> once and clears it.</summary>
    public string? TakeNotice()
    {
        var notice = PendingNotice;
        PendingNotice = null;
        return notice;
    }

    /// <summary>True when the Log in page should show "Sign in with …": switched on, the phone can scan, and a
    /// sign-in is kept on the phone.</summary>
    public async Task<bool> IsReadyAsync() =>
        IsOn && Status == BiometricStatus.Available && await _tokens.HasSavedSessionAsync();

    /// <summary>
    /// Asks for a scan before the saved sign-in is used. When the phone's fingerprints / faces changed (or were all
    /// removed) it switches off, deletes the saved sign-in and leaves <see cref="ChangedMessage"/> in
    /// <see cref="PendingNotice"/>.
    /// </summary>
    public async Task<BiometricOutcome> UnlockAsync()
    {
        var outcome = Status switch
        {
            BiometricStatus.Available => await _biometrics.AuthenticateAsync("Sign in to Phloem"),
            BiometricStatus.NotSetUp => BiometricOutcome.Changed,   // every fingerprint / face was removed
            _ => BiometricOutcome.Failed                            // sensor busy or gone: use the password
        };

        if (outcome == BiometricOutcome.Changed)
        {
            TurnOff();
            await _tokens.ClearAsync();
            PendingNotice = ChangedMessage;
        }

        return outcome;
    }

    /// <summary>
    /// Switches it on for the signed-in account: one scan to confirm, then the sign-in is kept on the phone (even if
    /// "Remember me" wasn't ticked).
    /// </summary>
    /// <returns><see cref="BiometricOutcome.Success"/> when it's on.</returns>
    public async Task<BiometricOutcome> TurnOnAsync()
    {
        if (_session.Profile is not { } profile || !_tokens.CanRefresh) return BiometricOutcome.Failed;
        if (Status != BiometricStatus.Available) return BiometricOutcome.Failed;

        var outcome = await _biometrics.EnrolAsync($"Confirm it's you to sign in with {DisplayName} next time");
        if (outcome != BiometricOutcome.Success)
        {
            _biometrics.Forget();
            return outcome;
        }

        _tokens.KeepStrictOnDevice = true;
        await _tokens.KeepOnDeviceAsync();
        _settings.TurnOn(profile.Id);
        return BiometricOutcome.Success;
    }

    /// <summary>Switches it off (the sign-in itself is left alone). Safe to call any time.</summary>
    /// <remarks>For a seller / staff sign-in the saved copy is removed too: without the scan they sign in again
    /// when the app closes.</remarks>
    public void TurnOff()
    {
        _settings.TurnOff();
        _biometrics.Forget();
        _tokens.KeepStrictOnDevice = false;
        _ = _tokens.ForgetStrictOnDeviceAsync();
    }

    /// <summary>True when the offer after login should be shown to this account.</summary>
    /// <param name="userId">The account that just signed in.</param>
    public bool ShouldOffer(Guid userId) =>
        Status == BiometricStatus.Available && !IsOnFor(userId) && !_settings.HasDeclined(userId);

    /// <summary>Remembers "Not now" so this account isn't asked at every login.</summary>
    /// <param name="userId">The account that just signed in.</param>
    public void Decline(Guid userId) => _settings.Decline(userId);
}
