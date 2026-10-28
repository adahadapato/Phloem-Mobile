namespace Phloem.Services;

/// <summary>
/// "Keep screen on": stops the phone dimming and locking while someone is using a busy screen (the seller's orders,
/// a courier's deliveries, order tracking, checkout). Switched on by default; the person can turn it off in
/// My account → Settings. Everywhere else the phone locks as usual, so the battery isn't wasted.
/// </summary>
/// <remarks>
/// <para>Screens ask for it with <see cref="Helpers.KeepScreenAwakeBehavior"/>, which calls <see cref="Hold"/> when the
/// screen appears and <see cref="Release"/> when it goes. It's counted, so moving between two such screens never
/// lets the phone dim in between.</para>
/// <para>Uses MAUI's <c>DeviceDisplay.KeepScreenOn</c>: no camera, no permission, and the OS clears it when the app
/// goes to the background. Register as a singleton.</para>
/// </remarks>
public sealed class ScreenAwakeService
{
    private const string PreferenceKey = "keep_screen_on";
    private int _holders;

    /// <summary>The person's choice (default: on).</summary>
    public bool IsEnabled
    {
        get
        {
            try { return Preferences.Default.Get(PreferenceKey, true); }
            catch (Exception) { return true; }
        }
        set
        {
            try { Preferences.Default.Set(PreferenceKey, value); }
            catch (Exception) { /* not saved: still applied for now */ }
            Apply();
        }
    }

    /// <summary>A busy screen appeared.</summary>
    public void Hold()
    {
        Interlocked.Increment(ref _holders);
        Apply();
    }

    /// <summary>That screen went away.</summary>
    public void Release()
    {
        if (Interlocked.Decrement(ref _holders) < 0) Interlocked.Exchange(ref _holders, 0);
        Apply();
    }

    private void Apply()
    {
        var keepOn = IsEnabled && Volatile.Read(ref _holders) > 0;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try { DeviceDisplay.Current.KeepScreenOn = keepOn; }
            catch (Exception) { /* not supported on this device: the screen just behaves normally */ }
        });
    }
}
