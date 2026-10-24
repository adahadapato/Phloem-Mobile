namespace Phloem.Settings;

/// <summary>
/// Every configuration key the app reads (Settings/appsettings.json and, in Debug builds,
/// Settings/appsettings.Development.json), in one place so no code repeats "magic strings".
/// </summary>
/// <remarks>
/// <para><b>Rule:</b> when a setting is added to appsettings.json, add its key here in the same change.</para>
/// <para>Same rules as the API's <c>Phloem.Domain.Constants.AppSettings</c>: class nesting mirrors JSON nesting,
/// constant name = last key segment, every level has <c>Section</c>.</para>
/// <para>The app holds NO secrets: anything in these files ships inside the app and can be read by anyone.</para>
/// </remarks>
public static class AppSettings
{
    /// <summary>File names of the settings files (embedded in the app, see Phloem.csproj).</summary>
    public static class Files
    {
        /// <summary>Always loaded: release values.</summary>
        public const string Default = "appsettings.json";

        /// <summary>Loaded on top in Debug builds only: your local API.</summary>
        public const string Development = "appsettings.Development.json";
    }

    // ==================== API ====================

    /// <summary>Where the Phloem API is. Bound to <see cref="ApiSettings"/>.</summary>
    public static class Api
    {
        /// <summary>The whole section.</summary>
        public const string Section = "Api";

        /// <summary>The API's address, without a trailing slash (e.g. "https://phloemapi.phloemmarket.app").</summary>
        public const string BaseUrl = "Api:BaseUrl";

        /// <summary>
        /// Address used on the ANDROID EMULATOR only (e.g. "https://10.0.2.2:7103": the emulator reaches your PC's
        /// localhost through 10.0.2.2). Empty = use <see cref="BaseUrl"/>.
        /// </summary>
        public const string AndroidEmulatorBaseUrl = "Api:AndroidEmulatorBaseUrl";

        /// <summary>
        /// Android emulator only: where pictures are loaded from (e.g. "http://10.0.2.2:5175"), because the local API's
        /// photo links say "localhost". Empty = use the links as they are.
        /// </summary>
        public const string AndroidEmulatorFilesBaseUrl = "Api:AndroidEmulatorFilesBaseUrl";

        /// <summary>How long one request may take, in seconds.</summary>
        public const string TimeoutSeconds = "Api:TimeoutSeconds";

        /// <summary>
        /// Debug only: accept the local ASP.NET development certificate (which the emulator doesn't trust).
        /// Ignored in Release builds.
        /// </summary>
        public const string TrustDevelopmentCertificate = "Api:TrustDevelopmentCertificate";
    }

    // ==================== Paging ====================

    /// <summary>Long lists shown a page at a time. Bound to <see cref="PagingSettings"/>.</summary>
    public static class Paging
    {
        /// <summary>The whole section.</summary>
        public const string Section = "Paging";

        /// <summary>The "Show" choices people can pick from, e.g. [10, 15, 20, 30, 50] (the API allows up to 100).</summary>
        public const string SizeOptions = "Paging:SizeOptions";

        /// <summary>
        /// Items per page until the person picks their own, per list (keys are <see cref="PagedLists"/>),
        /// e.g. "Paging:Defaults:Dishes" = 20.
        /// </summary>
        public const string Defaults = "Paging:Defaults";
    }
}
