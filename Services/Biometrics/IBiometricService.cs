namespace Phloem.Services.Biometrics;

/// <summary>
/// The phone's own fingerprint / Face ID check. One implementation per platform:
/// <c>Platforms/Android/Biometrics/AndroidBiometricService</c> (BiometricPrompt + a Keystore key),
/// <c>Platforms/iOS/Biometrics/IosBiometricService</c> (LocalAuthentication), and
/// <see cref="NoBiometricService"/> everywhere else.
/// </summary>
/// <remarks>
/// <para>Phloem never sees a fingerprint or a face: the phone only answers "yes, it's the owner" or "no".</para>
/// <para>Only the phone's biometrics are accepted, never the phone's PIN or passcode as a fallback: someone who knows
/// the PIN must still type the Phloem password.</para>
/// <para>Each platform remembers the phone's biometric "state" when sign-in by scan is switched on
/// (<see cref="EnrolAsync"/>); if a fingerprint or face is added or all are removed afterwards,
/// <see cref="AuthenticateAsync"/> answers <see cref="BiometricOutcome.Changed"/>.</para>
/// </remarks>
public interface IBiometricService
{
    /// <summary>What the phone can do right now (checked fresh: the person may have just set up a fingerprint).</summary>
    BiometricStatus GetStatus();

    /// <summary>The name to show: "Face ID", "Touch ID", "fingerprint" or "fingerprint or face".</summary>
    string DisplayName { get; }

    /// <summary>True when the app can open the phone's own "add a fingerprint / face" screen (Android).</summary>
    bool CanOpenSetup { get; }

    /// <summary>Opens the phone's "add a fingerprint / face" screen (does nothing where <see cref="CanOpenSetup"/> is
    /// false).</summary>
    void OpenSetup();

    /// <summary>
    /// Switching sign-in by scan ON: starts a fresh lock tied to the fingerprints / faces on the phone now, then asks
    /// for one scan to confirm it's the owner.
    /// </summary>
    /// <param name="reason">The line shown in the prompt.</param>
    /// <returns><see cref="BiometricOutcome.Success"/> when switched on; anything else leaves it off.</returns>
    Task<BiometricOutcome> EnrolAsync(string reason);

    /// <summary>Signing in: asks for a scan and checks the lock from <see cref="EnrolAsync"/> is still valid.</summary>
    /// <param name="reason">The line shown in the prompt.</param>
    Task<BiometricOutcome> AuthenticateAsync(string reason);

    /// <summary>Switching OFF: destroys the lock (Android key / iOS saved state). Safe to call any time.</summary>
    void Forget();
}
