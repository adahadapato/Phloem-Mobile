namespace Phloem.Services.Biometrics;

/// <summary>
/// What this phone remembers about fingerprint / Face ID sign-in (in Preferences: nothing here is secret).
/// </summary>
/// <remarks>
/// <para><b>OnForUserId:</b> whose account sign-in by scan is switched on for (null = off). The tokens themselves
/// stay where they always were (<c>TokenStore</c>, in the phone's secure storage).</para>
/// <para><b>Declined:</b> the accounts that tapped "Not now" on the offer after login, so they aren't asked at every
/// login (they can still switch it on in My account).</para>
/// </remarks>
public sealed class BiometricSettings
{
    private const string OnForUserKey = "phloem.biometrics.user";
    private const string DeclinedKeyPrefix = "phloem.biometrics.declined.";

    /// <summary>The account sign-in by scan is on for; null when it's off.</summary>
    public Guid? OnForUserId =>
        Guid.TryParse(Preferences.Default.Get<string?>(OnForUserKey, null), out var id) ? id : null;

    /// <summary>True when sign-in by scan is on (for whichever account last switched it on).</summary>
    public bool IsOn => OnForUserId is not null;

    /// <summary>Switches it on for this account.</summary>
    /// <param name="userId">The signed-in account.</param>
    public void TurnOn(Guid userId)
    {
        Preferences.Default.Set(OnForUserKey, userId.ToString());
        Preferences.Default.Remove(DeclinedKeyPrefix + userId);
    }

    /// <summary>Switches it off.</summary>
    public void TurnOff() => Preferences.Default.Remove(OnForUserKey);

    /// <summary>True when this account said "Not now" to the offer.</summary>
    /// <param name="userId">The signed-in account.</param>
    public bool HasDeclined(Guid userId) => Preferences.Default.Get(DeclinedKeyPrefix + userId, false);

    /// <summary>Remembers "Not now" for this account.</summary>
    /// <param name="userId">The signed-in account.</param>
    public void Decline(Guid userId) => Preferences.Default.Set(DeclinedKeyPrefix + userId, true);
}
