using Android.App;
using Android.Content;
using Android.Content.PM;
using Microsoft.Maui.Authentication;
using Phloem.Services;

namespace Phloem;

/// <summary>
/// Receives <c>phloem://auth?…</c> when the API sends the browser back after Google, and hands it to
/// <see cref="WebAuthenticator"/> (which <c>AuthService.SignInWithGoogleAsync</c> is waiting on). Then it closes.
/// </summary>
/// <remarks>
/// <para><b>Exported</b> so the browser (Chrome Custom Tabs) can open it; <b>NoHistory</b> so it never stays behind in
/// the back stack.</para>
/// <para>The scheme and host come from <see cref="ExternalSignIn"/>, the one place they are written.</para>
/// </remarks>
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter([Intent.ActionView],
              Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
              DataScheme = ExternalSignIn.CallbackScheme,
              DataHost = ExternalSignIn.CallbackHost)]
public class WebAuthenticationCallbackActivity : WebAuthenticatorCallbackActivity
{
}
