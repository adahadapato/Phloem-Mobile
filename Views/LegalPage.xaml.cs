using Phloem.Helpers;
using Phloem.ViewModels;

namespace Phloem.Views;

public partial class LegalPage : ContentPage
{
    readonly LegalViewModel _vm;
    bool _loaded;

    public LegalPage(LegalViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_loaded) return;
        _loaded = true;
        await _vm.LoadAsync();
    }

    // ← back to wherever the link was tapped
    async void OnBackTapped(object? sender, TappedEventArgs e)
        => await Shell.Current.GoToAsync("..");

    /// <summary>
    /// A link in the text: "/terms", "/privacy" and "/help" (with or without #section) open here in the app, and so
    /// do website pages the app also has (My orders, Send food…); anything else opens in the browser / mail app.
    /// The WebView itself never leaves the page.
    /// </summary>
    /// <summary>The app's own screen for a website page linked from the Help text (null: open the website).</summary>
    static string? AppRouteFor(string path) => path switch
    {
        "/send-parcel" => "international",
        "/orders" => "//orders",
        "/shipments" => "//account/myshipments",
        "/account" => "//account",
        "/account/password" => "//account/changepassword",
        "/sell" => "sellersetup",
        "/about" => "about",
        _ => null
    };

    async void OnLinkTapped(object? sender, WebNavigatingEventArgs e)
    {
        // The page's own HTML loads normally; only links the user taps are handled here
        if (!Uri.TryCreate(e.Url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("https" or "http" or "mailto" or "tel"))
            return;

        e.Cancel = true;
        var section = uri.Fragment.TrimStart('#');
        var path = uri.AbsolutePath.TrimEnd('/');
        var ours = uri.Host.EndsWith(SimpleMarkdown.SiteHost, StringComparison.OrdinalIgnoreCase);

        var inApp = ours ? AppRouteFor(path.ToLowerInvariant()) : null;
        if (ours && path.Equals("/terms", StringComparison.OrdinalIgnoreCase))
            await AppNav.OpenLegalAsync("Terms", section);
        else if (ours && path.Equals("/privacy", StringComparison.OrdinalIgnoreCase))
            await AppNav.OpenLegalAsync("Privacy", section);
        else if (ours && path.Equals("/help", StringComparison.OrdinalIgnoreCase))
            await AppNav.OpenLegalAsync("Help", section);
        else if (inApp is not null)
            await Shell.Current.GoToAsync(inApp);
        else
        {
            try { await Launcher.Default.OpenAsync(uri); }
            catch (Exception) { /* no app can open it */ }
        }
    }
}
