using Phloem.Views;

namespace Phloem;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    /// <summary>Created by MAUI from the service container.</summary>
    /// <param name="services">Used to create the start-up page.</param>
    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    /// <summary>
    /// Opens on <see cref="StartupPage"/>: it loads the countries from the API and restores the sign-in, then
    /// switches the window to <see cref="AppShell"/>.
    /// </summary>
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_services.GetRequiredService<StartupPage>())
        {
            // Sensible default desktop window size; MAUI still fully adapts
            // to phones/tablets since Shell/FlexLayout/Grid are all responsive.
            Width = 1200,
            Height = 800,
            MinimumWidth = 380,
            MinimumHeight = 640
        };

        // Sellers and staff: sign in again after too long in the background
        _services.GetRequiredService<Phloem.Services.SessionLock>().Attach(window);
        return window;
    }
}
