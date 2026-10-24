namespace Phloem.Services;

/// <summary>
/// The address the API sends the browser back to after Google: <c>phloem://auth</c>. Kept in ONE place.
/// </summary>
/// <remarks>
/// <para><b>Why a constant, not a setting:</b> Android and iOS register the <c>phloem</c> scheme when the app is BUILT
/// (the <c>WebAuthenticationCallbackActivity</c> attribute on Android, <c>CFBundleURLSchemes</c> in Info.plist on iOS),
/// so it can't change at run time. If it ever changes, change it here, in Info.plist, and in the API's
/// <c>Authentication:AppCallbackUrl</c>.</para>
/// <para>The API adds <c>?code=…</c> on success, or <c>?error=…&amp;message=…</c> on failure.</para>
/// </remarks>
public static class ExternalSignIn
{
    /// <summary>The app's own link scheme.</summary>
    public const string CallbackScheme = "phloem";

    /// <summary>The part after the scheme.</summary>
    public const string CallbackHost = "auth";

    /// <summary>The full address, <c>phloem://auth</c>.</summary>
    public static Uri CallbackUri { get; } = new($"{CallbackScheme}://{CallbackHost}");

    /// <summary>Query value: the one-time code (success).</summary>
    public const string CodeKey = "code";

    /// <summary>Query value: the API's error code (failure), e.g. "Auth.ExternalLoginFailed".</summary>
    public const string ErrorKey = "error";

    /// <summary>Query value: the API's message for the error, ready to show.</summary>
    public const string MessageKey = "message";

    /// <summary>The app's own code when the person closed the sign-in sheet.</summary>
    public const string CancelledCode = "Auth.ExternalLoginCancelled";
}
