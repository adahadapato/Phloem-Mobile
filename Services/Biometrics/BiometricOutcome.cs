namespace Phloem.Services.Biometrics;

/// <summary>How a fingerprint / Face ID check ended.</summary>
public enum BiometricOutcome
{
    /// <summary>The phone confirmed it's the owner.</summary>
    Success,

    /// <summary>The person closed the prompt or tapped "Use password" (nothing to report).</summary>
    Cancelled,

    /// <summary>The check couldn't be done (too many tries, sensor busy, unexpected error).</summary>
    Failed,

    /// <summary>
    /// The phone's fingerprints or faces changed since biometric sign-in was switched on (one added or all removed).
    /// The phone itself has destroyed the lock, so sign-in by scan is switched off and the person must sign in with
    /// their password again.
    /// </summary>
    Changed
}
