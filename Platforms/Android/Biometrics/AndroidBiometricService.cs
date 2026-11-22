using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Security.Keystore;
using AndroidX.Biometric;
using AndroidX.Core.Content;
using Java.Security;
using Javax.Crypto;

namespace Phloem.Services.Biometrics;

/// <summary>
/// Fingerprint / face sign-in on Android: AndroidX <see cref="BiometricPrompt"/> with a key in the phone's Keystore.
/// </summary>
/// <remarks>
/// <para><b>The key</b> (<see cref="KeyAlias"/>) is made when sign-in by scan is switched on. Android only lets it be
/// used straight after a successful STRONG biometric scan (Class 3: fingerprint, and the secure face sensors), and
/// destroys it by itself when a fingerprint or face is added or all are removed
/// (<c>setInvalidatedByBiometricEnrollment</c>). Each scan is tied to the key (<see cref="BiometricPrompt.CryptoObject"/>)
/// and the key is then used once, so a scan can't be faked by another app.</para>
/// <para>The phone's PIN / pattern is NOT accepted instead of a scan (no device-credential fallback).</para>
/// <para>Needs Android 7.0 (API 24) or later; older phones get <see cref="BiometricStatus.NoSensor"/>.</para>
/// </remarks>
public sealed class AndroidBiometricService : IBiometricService
{
    private const string KeyStoreName = "AndroidKeyStore";
    private const string KeyAlias = "phloem.biometric_sign_in";
    private const string Transformation = "AES/GCM/NoPadding";

    // androidx.biometric constants, written out so they don't depend on how the binding names them
    private const int BiometricStrong = 0x000F;                 // BiometricManager.Authenticators.BIOMETRIC_STRONG
    private const int CanAuthenticateSuccess = 0;               // BIOMETRIC_SUCCESS
    private const int CanAuthenticateNoneEnrolled = 11;         // BIOMETRIC_ERROR_NONE_ENROLLED
    private const int ErrorCanceled = 5;                        // ERROR_CANCELED (system closed it)
    private const int ErrorUserCanceled = 10;                   // ERROR_USER_CANCELED
    private const int ErrorNegativeButton = 13;                 // ERROR_NEGATIVE_BUTTON ("Use password")

    /// <inheritdoc />
    public BiometricStatus GetStatus()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.N) return BiometricStatus.NoSensor;

        var context = Microsoft.Maui.ApplicationModel.Platform.AppContext;
        return BiometricManager.From(context).CanAuthenticate(BiometricStrong) switch
        {
            CanAuthenticateSuccess => BiometricStatus.Available,
            CanAuthenticateNoneEnrolled => BiometricStatus.NotSetUp,
            _ => BiometricStatus.NoSensor   // no hardware, hardware busy, security update needed, unsupported
        };
    }

    /// <inheritdoc />
    public string DisplayName
    {
        get
        {
            var pm = Microsoft.Maui.ApplicationModel.Platform.AppContext.PackageManager;
            var fingerprint = pm?.HasSystemFeature(PackageManager.FeatureFingerprint) ?? false;
            var face = Build.VERSION.SdkInt >= BuildVersionCodes.Q &&
                       (pm?.HasSystemFeature(PackageManager.FeatureFace) ?? false);
            return fingerprint && face ? "fingerprint or face"
                 : face ? "face unlock"
                 : "fingerprint";
        }
    }

    /// <inheritdoc />
    public bool CanOpenSetup => Build.VERSION.SdkInt >= BuildVersionCodes.P;

    /// <inheritdoc />
    public void OpenSetup()
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        if (activity is null) return;

        Intent intent;
        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            intent = new Intent(Android.Provider.Settings.ActionBiometricEnroll);
            intent.PutExtra(Android.Provider.Settings.ExtraBiometricAuthenticatorsAllowed, BiometricStrong);
        }
        else if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
        {
#pragma warning disable CA1422 // the older screen, only used on Android 9 and 10
            intent = new Intent(Android.Provider.Settings.ActionFingerprintEnroll);
#pragma warning restore CA1422
        }
        else
        {
            intent = new Intent(Android.Provider.Settings.ActionSecuritySettings);
        }

        try
        {
            activity.StartActivity(intent);
        }
        catch (ActivityNotFoundException)
        {
            // Some phones don't have the direct screen: open Security settings instead
            activity.StartActivity(new Intent(Android.Provider.Settings.ActionSecuritySettings));
        }
    }

    /// <inheritdoc />
    public async Task<BiometricOutcome> EnrolAsync(string reason)
    {
        if (GetStatus() != BiometricStatus.Available) return BiometricOutcome.Failed;

        try
        {
            Forget();
            CreateKey();
        }
        catch (Exception)
        {
            return BiometricOutcome.Failed;
        }

        var outcome = await AuthenticateAsync(reason);
        return outcome == BiometricOutcome.Changed ? BiometricOutcome.Failed : outcome;
    }

    /// <inheritdoc />
    public async Task<BiometricOutcome> AuthenticateAsync(string reason)
    {
        Cipher cipher;
        try
        {
            if (LoadKey() is not { } key) return BiometricOutcome.Changed;   // gone (e.g. app data restored)

            cipher = Cipher.GetInstance(Transformation)!;
            cipher.Init(CipherMode.EncryptMode, key);
        }
        catch (KeyPermanentlyInvalidatedException)
        {
            return BiometricOutcome.Changed;   // a fingerprint / face was added or all removed
        }
        catch (Exception)
        {
            return BiometricOutcome.Failed;
        }

        return await MainThread.InvokeOnMainThreadAsync(() => ShowPromptAsync(cipher, reason));
    }

    /// <inheritdoc />
    public void Forget()
    {
        try
        {
            var keyStore = KeyStore.GetInstance(KeyStoreName)!;
            keyStore.Load(null);
            if (keyStore.ContainsAlias(KeyAlias)) keyStore.DeleteEntry(KeyAlias);
        }
        catch (Exception)
        {
            // Nothing to delete
        }
    }

    // ───────────────────────────── Helpers ─────────────────────────────

    /// <summary>Shows the system prompt (main thread) and waits for the answer.</summary>
    private static Task<BiometricOutcome> ShowPromptAsync(Cipher cipher, string reason)
    {
        if (Microsoft.Maui.ApplicationModel.Platform.CurrentActivity is not AndroidX.Fragment.App.FragmentActivity activity)
            return Task.FromResult(BiometricOutcome.Failed);

        var callback = new BiometricPromptCallback();
        var prompt = new BiometricPrompt(activity, ContextCompat.GetMainExecutor(activity), callback);

        var info = new BiometricPrompt.PromptInfo.Builder()
            .SetTitle("Phloem")
            .SetSubtitle(reason)
            .SetNegativeButtonText("Use password")
            .SetAllowedAuthenticators(BiometricStrong)
            .SetConfirmationRequired(false)
            .Build();

        prompt.Authenticate(info, new BiometricPrompt.CryptoObject(cipher));
        return callback.Result;
    }

    /// <summary>Turns the prompt's error code into an outcome.</summary>
    /// <param name="errorCode">BiometricPrompt.ERROR_*.</param>
    internal static BiometricOutcome FromError(int errorCode) => errorCode switch
    {
        ErrorUserCanceled or ErrorNegativeButton or ErrorCanceled => BiometricOutcome.Cancelled,
        CanAuthenticateNoneEnrolled => BiometricOutcome.Changed,   // ERROR_NO_BIOMETRICS: all were removed
        _ => BiometricOutcome.Failed                               // lockout, hardware busy, timeout…
    };

    /// <summary>Makes the key: AES, usable only straight after a strong biometric scan, destroyed when the phone's
    /// fingerprints / faces change.</summary>
    private static void CreateKey()
    {
        // No validity time set = the key works only for the one operation right after a strong biometric scan
        var builder = new KeyGenParameterSpec.Builder(KeyAlias, KeyStorePurpose.Encrypt | KeyStorePurpose.Decrypt)
            .SetBlockModes(KeyProperties.BlockModeGcm)
            .SetEncryptionPaddings(KeyProperties.EncryptionPaddingNone)
            .SetUserAuthenticationRequired(true)
            .SetInvalidatedByBiometricEnrollment(true);

        var generator = KeyGenerator.GetInstance(KeyProperties.KeyAlgorithmAes, KeyStoreName)!;
        generator.Init(builder.Build());
        generator.GenerateKey();
    }

    /// <summary>The key, or null when it doesn't exist.</summary>
    private static IKey? LoadKey()
    {
        var keyStore = KeyStore.GetInstance(KeyStoreName)!;
        keyStore.Load(null);
        return keyStore.GetKey(KeyAlias, null);
    }
}
