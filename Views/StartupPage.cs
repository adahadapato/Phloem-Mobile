using Microsoft.Extensions.Options;
using Phloem.Services;
using Phloem.Services.Biometrics;
using Phloem.Settings;

namespace Phloem.Views;

/// <summary>
/// Shown when the app opens, over the Phloem poster ("Discover African food, one bite closer."): loads what every
/// screen needs from the API (the countries), restores the user's sign-in, then opens the app (<see cref="AppShell"/>).
/// The poster stays at least <see cref="MinimumShowTime"/> so it never just flashes by.
/// </summary>
/// <remarks>
/// On a FIRST start without internet there are no countries yet, so it says so and offers "Try again".
/// After that, the countries kept on the phone are used and the app opens offline too.
/// </remarks>
public sealed class StartupPage : ContentPage
{
    private readonly CountryService _countries;
    private readonly AuthService _auth;
    private readonly BiometricSignIn _biometrics;
    private readonly ApiSettings _api;
    /// <summary>The poster stays on screen at least this long, even when loading is quicker.</summary>
    private static readonly TimeSpan MinimumShowTime = TimeSpan.FromSeconds(2);

    /// <summary>The app's green (BrandDark), the same as the phone's splash, behind the poster while it loads.</summary>
    private static readonly Color PosterGreen =
        Application.Current?.Resources.TryGetValue("BrandDark", out var brand) == true && brand is Color color
            ? color
            : Color.FromArgb("#043D1F");

    private readonly ActivityIndicator _spinner = new()
    {
        IsRunning = true, Color = Color.FromArgb("#F4A51A"), WidthRequest = 30, HeightRequest = 30
    };
    private readonly Label _message = new()
    {
        Text = "Loading Phloem…", TextColor = Colors.White, FontSize = 15,
        HorizontalTextAlignment = TextAlignment.Center
    };
    private readonly Label _details = new()
    {
        TextColor = Colors.White, Opacity = 0.6, FontSize = 12, HorizontalTextAlignment = TextAlignment.Center,
        IsVisible = false
    };
    private readonly Button _retry = new()
    {
        Text = "Try again", IsVisible = false, BackgroundColor = Color.FromArgb("#F4A51A"),
        TextColor = Color.FromArgb("#043D1F"), FontAttributes = FontAttributes.Bold, CornerRadius = 22,
        HeightRequest = 44, Padding = new Thickness(28, 0), HorizontalOptions = LayoutOptions.Center
    };
    private bool _started;

    /// <summary>Builds the page (no XAML): the poster, and at the bottom a spinner, a message and a retry button.</summary>
    public StartupPage(CountryService countries, AuthService auth, BiometricSignIn biometrics, IOptions<ApiSettings> api)
    {
        _countries = countries;
        _auth = auth;
        _biometrics = biometrics;
        _api = api.Value;

        BackgroundColor = PosterGreen;
        Shell.SetNavBarIsVisible(this, false);

        _retry.Clicked += async (_, _) => await StartAsync();

        // Status at the bottom, on a soft dark fade so it reads over the food
        var status = new Grid
        {
            VerticalOptions = LayoutOptions.End,
            Padding = new Thickness(32, 60, 32, 48),
            Background = new LinearGradientBrush(
                new GradientStopCollection
                {
                    new GradientStop(Color.FromArgb("#00012411"), 0f),
                    new GradientStop(Color.FromArgb("#E6012411"), 0.55f)
                },
                new Point(0, 0), new Point(0, 1)),
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 12,
                    HorizontalOptions = LayoutOptions.Center,
                    Children = { _spinner, _message, _details, _retry }
                }
            }
        };

        Content = new Grid
        {
            Children =
            {
                new Image { Source = "splash_poster.jpg", Aspect = Aspect.AspectFill },
                status
            }
        };
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_started) return;
        _started = true;
        await StartAsync();
    }

    /// <summary>Loads the countries and the sign-in, then opens the app.</summary>
    private async Task StartAsync()
    {
        _retry.IsVisible = false;
        _details.IsVisible = false;
        _spinner.IsRunning = true;
        _message.Text = "Loading Phloem…";
        var shown = System.Diagnostics.Stopwatch.StartNew();

        if (!await _countries.LoadAsync())
        {
            _spinner.IsRunning = false;
            _message.Text = _countries.LastError?.DisplayMessage
                            ?? "We couldn't reach Phloem. Please check your internet connection.";

            // Small print that says WHY (e.g. "Network.Unreachable" or "HTTP 500") and which address was called,
            // so a wrong address or a server problem can be told apart from no internet
            var error = _countries.LastError;
            var reason = error is null ? "No countries" : error.Status > 0 ? $"HTTP {error.Status} · {error.Code}" : error.Code;
            _details.Text = $"{reason}\n{ServerAddress()}";
            _details.IsVisible = true;
            _retry.IsVisible = true;
            return;
        }

        try
        {
            // Signed back in if "Remember me" was ticked. With Face ID / fingerprint sign-in on, the scan is asked
            // for here; closing it opens the app signed out (Log in then offers "Sign in with …").
            await _auth.RestoreSessionAsync();
        }
        catch (Exception)
        {
            // Never block the app because of the sign-in: the user can sign in by hand
        }

        // The phone's fingerprints / faces changed: the saved sign-in was deleted, so say why they're signed out
        if (_biometrics.TakeNotice() is { } notice)
            await DisplayAlertAsync("Please sign in again", notice, "OK");

        // Let the poster be seen, even on a fast connection
        var left = MinimumShowTime - shown.Elapsed;
        if (left > TimeSpan.Zero) await Task.Delay(left);

        if (Window is not null) Window.Page = new AppShell();
    }

    /// <summary>The API address this device calls (shown when start-up fails).</summary>
    private string ServerAddress()
    {
        try { return _api.ResolveBaseAddress().ToString(); }
        catch (InvalidOperationException ex) { return ex.Message; }
    }
}
