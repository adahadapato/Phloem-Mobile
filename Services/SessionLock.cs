using Phloem.Services.Api;
using Phloem.Services.Biometrics;

namespace Phloem.Services;

/// <summary>
/// The background lock for sellers, couriers, admins and staff: when the app comes back after being in the background
/// longer than the API's <see cref="TokenStore.LockAfterMinutes"/> (15 min staff, 60 min sellers; none for buyers),
/// the user is signed out and taken to Log in.
/// </summary>
/// <remarks>
/// <para>Uses <see cref="AuthService.LogoutAsync"/>, so with fingerprint / Face ID sign-in on it only LOCKS (the Log in
/// page asks for the scan at once); otherwise the sign-in is cancelled and the password is needed.</para>
/// <para>The app being fully closed needs nothing here: seller / staff sign-ins aren't kept on the phone
/// (<see cref="TokenStore.IsStrict"/>), unless behind the scan, which start-up asks for.</para>
/// <para>Attached to the window in <c>App.CreateWindow</c> (Stopped = went to the background, Resumed = came back).</para>
/// </remarks>
public sealed class SessionLock(TokenStore tokens, SessionService session, AuthService auth, BiometricSignIn biometrics)
{
    private DateTime? _awaySinceUtc;

    /// <summary>Starts watching the window.</summary>
    /// <param name="window">The app's window.</param>
    public void Attach(Window window)
    {
        window.Stopped += (_, _) => _awaySinceUtc = DateTime.UtcNow;
        window.Resumed += async (_, _) => await CheckAsync();
    }

    /// <summary>Back from the background: lock if it was away too long.</summary>
    private async Task CheckAsync()
    {
        var awaySince = _awaySinceUtc;
        _awaySinceUtc = null;

        if (awaySince is null || session.CurrentUser is null || tokens.LockAfterMinutes is not { } minutes) return;
        if (DateTime.UtcNow - awaySince.Value < TimeSpan.FromMinutes(minutes)) return;

        var quick = biometrics.IsOnFor(session.Profile?.Id);
        await auth.LogoutAsync();   // locks with quick sign-in on; otherwise signs out completely
        session.SignedOutReason = quick
            ? $"For your security, please sign in again with your {biometrics.DisplayName}."
            : "For your security you were signed out after a while away. Please sign in again.";

        try
        {
            await Shell.Current.GoToAsync("//account/login");
        }
        catch (Exception)
        {
            // Shell not ready (e.g. still on the start-up page): the next Log in visit shows the message
        }
    }
}
