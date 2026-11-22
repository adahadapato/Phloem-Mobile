namespace Phloem.Services.Biometrics;

/// <summary>What this phone can do for fingerprint / Face ID sign-in, checked fresh every time it matters.</summary>
public enum BiometricStatus
{
    /// <summary>A sensor is there and at least one fingerprint or face is set up: sign-in by scan can be used.</summary>
    Available,

    /// <summary>A sensor is there but nothing is set up yet: the person must add a fingerprint or face in the phone's
    /// settings first. The My account row shows greyed out with a hint.</summary>
    NotSetUp,

    /// <summary>No suitable sensor (or a platform the app doesn't support for this): every biometric button and row
    /// stays hidden.</summary>
    NoSensor
}
