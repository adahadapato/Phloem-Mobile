namespace Phloem.Services.Biometrics;

/// <summary>
/// Platforms without fingerprint / Face ID sign-in in Phloem (Windows, Mac): always <see cref="BiometricStatus.NoSensor"/>,
/// so every biometric button and row stays hidden.
/// </summary>
public sealed class NoBiometricService : IBiometricService
{
    /// <inheritdoc />
    public BiometricStatus GetStatus() => BiometricStatus.NoSensor;

    /// <inheritdoc />
    public string DisplayName => "fingerprint";

    /// <inheritdoc />
    public bool CanOpenSetup => false;

    /// <inheritdoc />
    public void OpenSetup() { }

    /// <inheritdoc />
    public Task<BiometricOutcome> EnrolAsync(string reason) => Task.FromResult(BiometricOutcome.Failed);

    /// <inheritdoc />
    public Task<BiometricOutcome> AuthenticateAsync(string reason) => Task.FromResult(BiometricOutcome.Failed);

    /// <inheritdoc />
    public void Forget() { }
}
