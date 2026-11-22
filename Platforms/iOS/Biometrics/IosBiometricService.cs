using Foundation;
using LocalAuthentication;

namespace Phloem.Services.Biometrics;

/// <summary>
/// Face ID / Touch ID sign-in on iPhone, with Apple's LocalAuthentication.
/// </summary>
/// <remarks>
/// <para>Only Face ID / Touch ID is accepted (<see cref="LAPolicy.DeviceOwnerAuthenticationWithBiometrics"/>): the
/// "Enter Passcode" fallback is hidden, so the phone's passcode can't be used instead.</para>
/// <para><b>Changes:</b> when switched on, the phone's biometric "state" (<see cref="LAContext.EvaluatedPolicyDomainState"/>,
/// which changes whenever a face or finger is added or removed) is saved in <see cref="StateKey"/>. Before each scan
/// it is compared; if it differs the answer is <see cref="BiometricOutcome.Changed"/>.</para>
/// <para>Needs <c>NSFaceIDUsageDescription</c> in Info.plist (the line iOS shows the first time).</para>
/// </remarks>
public sealed class IosBiometricService : IBiometricService
{
    private const string StateKey = "phloem.biometrics.ios_state";

    /// <inheritdoc />
    public BiometricStatus GetStatus()
    {
        using var context = new LAContext();
        if (context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out var error))
            return BiometricStatus.Available;

        return error?.Code == (long)LAStatus.BiometryNotEnrolled
            ? BiometricStatus.NotSetUp
            : BiometricStatus.NoSensor;   // no sensor, no passcode set, or locked out for now
    }

    /// <inheritdoc />
    public string DisplayName
    {
        get
        {
            using var context = new LAContext();
            context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out _);   // fills BiometryType
            return context.BiometryType switch
            {
                LABiometryType.FaceId => "Face ID",
                LABiometryType.TouchId => "Touch ID",
                _ => "Face ID"
            };
        }
    }

    /// <inheritdoc />
    /// <remarks>iOS doesn't let apps open the Face ID / Touch ID settings screen.</remarks>
    public bool CanOpenSetup => false;

    /// <inheritdoc />
    public void OpenSetup() { }

    /// <inheritdoc />
    public async Task<BiometricOutcome> EnrolAsync(string reason)
    {
        Forget();
        var (outcome, state) = await EvaluateAsync(reason);
        if (outcome == BiometricOutcome.Success && state is not null)
            Preferences.Default.Set(StateKey, state);
        return outcome == BiometricOutcome.Success && state is null ? BiometricOutcome.Failed : outcome;
    }

    /// <inheritdoc />
    public async Task<BiometricOutcome> AuthenticateAsync(string reason)
    {
        var saved = Preferences.Default.Get<string?>(StateKey, null);
        if (saved is null) return BiometricOutcome.Changed;

        // Compare BEFORE scanning: a newly added face must not be able to unlock
        using (var check = new LAContext())
        {
            if (check.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out var error))
            {
                if (StateOf(check) != saved) return BiometricOutcome.Changed;
            }
            else if (error?.Code == (long)LAStatus.BiometryNotEnrolled)
            {
                return BiometricOutcome.Changed;
            }
        }

        var (outcome, state) = await EvaluateAsync(reason);
        if (outcome == BiometricOutcome.Success && state != saved) return BiometricOutcome.Changed;
        return outcome;
    }

    /// <inheritdoc />
    public void Forget() => Preferences.Default.Remove(StateKey);

    // ───────────────────────────── Helpers ─────────────────────────────

    /// <summary>Shows the Face ID / Touch ID prompt.</summary>
    /// <returns>The outcome, and the phone's biometric state after a successful scan.</returns>
    private static async Task<(BiometricOutcome Outcome, string? State)> EvaluateAsync(string reason)
    {
        using var context = new LAContext
        {
            LocalizedFallbackTitle = string.Empty,   // hides "Enter Passcode"
            LocalizedCancelTitle = "Use password"
        };

        if (!context.CanEvaluatePolicy(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, out var canError))
            return (canError?.Code == (long)LAStatus.BiometryNotEnrolled ? BiometricOutcome.Changed : BiometricOutcome.Failed,
                    null);

        try
        {
            var (ok, error) = await MainThread.InvokeOnMainThreadAsync(() =>
                context.EvaluatePolicyAsync(LAPolicy.DeviceOwnerAuthenticationWithBiometrics, reason));

            if (ok) return (BiometricOutcome.Success, StateOf(context));

            return ((LAStatus)(error?.Code ?? 0) switch
            {
                LAStatus.UserCancel or LAStatus.SystemCancel or LAStatus.AppCancel or LAStatus.UserFallback
                    => BiometricOutcome.Cancelled,
                LAStatus.BiometryNotEnrolled => BiometricOutcome.Changed,
                _ => BiometricOutcome.Failed   // locked out, not available…
            }, null);
        }
        catch (Exception)
        {
            return (BiometricOutcome.Failed, null);
        }
    }

    /// <summary>The phone's biometric state as text (changes when a face or finger is added or removed).</summary>
    private static string? StateOf(LAContext context) =>
#pragma warning disable CA1422 // still works on iOS 18; its replacement (DomainState) needs iOS 18+
        context.EvaluatedPolicyDomainState?.GetBase64EncodedString(NSDataBase64EncodingOptions.None);
#pragma warning restore CA1422
}
