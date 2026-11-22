using AndroidX.Biometric;
using Java.Lang;

namespace Phloem.Services.Biometrics;

/// <summary>
/// Receives the answer from Android's <see cref="BiometricPrompt"/> and turns it into a <see cref="Task"/>
/// (<see cref="Result"/>) for <see cref="AndroidBiometricService"/>.
/// </summary>
/// <remarks>
/// <para>A successful scan is double-checked by using the key it unlocked once (<c>Cipher.doFinal</c>): if the key
/// wasn't really unlocked, that throws and the answer is <see cref="BiometricOutcome.Failed"/>.</para>
/// <para>A finger that doesn't match (<see cref="OnAuthenticationFailed"/>) is NOT the end: the prompt stays open and
/// Android lets the person try again, then reports a lockout as an error.</para>
/// </remarks>
internal sealed class BiometricPromptCallback : BiometricPrompt.AuthenticationCallback
{
    private readonly TaskCompletionSource<BiometricOutcome> _result =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Completes when the prompt closes.</summary>
    public Task<BiometricOutcome> Result => _result.Task;

    /// <inheritdoc />
    public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result)
    {
        base.OnAuthenticationSucceeded(result);
        try
        {
            var cipher = result.CryptoObject?.Cipher;
            if (cipher is null)
            {
                _result.TrySetResult(BiometricOutcome.Failed);
                return;
            }

            cipher.DoFinal(new byte[16]);   // only works if the scan really unlocked the key
            _result.TrySetResult(BiometricOutcome.Success);
        }
        catch (System.Exception)
        {
            _result.TrySetResult(BiometricOutcome.Failed);
        }
    }

    /// <inheritdoc />
    public override void OnAuthenticationError(int errorCode, ICharSequence errString)
    {
        base.OnAuthenticationError(errorCode, errString);
        _result.TrySetResult(AndroidBiometricService.FromError(errorCode));
    }
}
