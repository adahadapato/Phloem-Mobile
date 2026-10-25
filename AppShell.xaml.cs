using Phloem.Services;
using Phloem.Views;

namespace Phloem;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // Pages that open on top of a tab (so the bottom bar stays visible).
        // These names must NOT also be used as a ShellContent Route in AppShell.xaml.
        Routing.RegisterRoute("about", typeof(AboutPage));
        Routing.RegisterRoute("legal", typeof(LegalPage));    // ?type=Terms|Privacy&section=sellers
        Routing.RegisterRoute("vendors", typeof(VendorsListPage));
        Routing.RegisterRoute("sellersetup", typeof(SellerSetupPage));
        Routing.RegisterRoute("dashboard", typeof(SellerDashboardPage));
        Routing.RegisterRoute("admin", typeof(AdminPage));
        Routing.RegisterRoute("register", typeof(RegisterPage));
        Routing.RegisterRoute("login", typeof(LoginPage));               // opened from My Account
        Routing.RegisterRoute("international", typeof(InternationalDeliveryPage)); // Send Food to UK
        Routing.RegisterRoute("address", typeof(DeliveryAddressPage));       // pick / add a delivery address
        Routing.RegisterRoute("orderdetails", typeof(OrderDetailsPage));          // one placed order
        Routing.RegisterRoute("dishedit", typeof(DishEditorPage));            // seller adds / edits a dish
        Routing.RegisterRoute("editprofile", typeof(EditProfilePage));           // My account → Edit profile
        Routing.RegisterRoute("changepassword", typeof(ChangePasswordPage));        // My account → Change password
        Routing.RegisterRoute("notifications", typeof(NotificationSettingsPage));  // My account → Notifications
        Routing.RegisterRoute("shopsettings", typeof(ShopSettingsPage));          // Seller dashboard → Shop settings
        Routing.RegisterRoute("myshipments", typeof(MyShipmentsPage));           // my parcels (Send Food to UK)
        Routing.RegisterRoute("shipmentdetails", typeof(ShipmentDetailsPage));   // one parcel: status + timeline
        Routing.RegisterRoute("adminroutes", typeof(AdminShipmentRoutesPage));   // Admin → Shipping routes
        Routing.RegisterRoute("adminshipments", typeof(AdminShipmentsPage));     // Admin → All shipments
        Routing.RegisterRoute("adminshop", typeof(AdminShopDetailsPage));        // Admin → one shop + documents
        Routing.RegisterRoute("shoporder", typeof(ShopOrderDetailsPage));        // Seller dashboard → one order
        Routing.RegisterRoute("admindishes", typeof(AdminDishCatalogPage));      // Admin → dish names & price ranges

        // Checkout (was already reached with GoToAsync(nameof(CheckoutPage))).
        Routing.RegisterRoute(nameof(CheckoutPage), typeof(CheckoutPage));

        // Side menu follows who is signed in (and changes as soon as they sign in or out)
        _session = IPlatformApplication.Current?.Services.GetService<SessionService>();
        if (_session is not null) _session.Changed += OnSessionChanged;
        UpdateMenu();
    }

    // ------------------------------------------------------------------
    // Side menu by who is signed in:
    //   Sign Up           only when signed out
    //   My Shipments      only when signed in
    //   Admin Dashboard   only for admins
    //   Become a Seller   hidden once the user has a shop
    //   Seller Dashboard  only when the user has a shop
    // ------------------------------------------------------------------
    private readonly SessionService? _session;

    private void OnSessionChanged(object? sender, EventArgs e) => MainThread.BeginInvokeOnMainThread(UpdateMenu);

    private void UpdateMenu()
    {
        var signedIn = _session?.IsLoggedIn ?? false;
        var hasShop = _session?.HasShop ?? false;

        SetFlyoutItemIsVisible(SignUpMenuItem, !signedIn);
        SetFlyoutItemIsVisible(MyShipmentsMenuItem, signedIn);
        SetFlyoutItemIsVisible(AdminMenuItem, _session?.IsAdmin ?? false);
        SetFlyoutItemIsVisible(BecomeSellerMenuItem, !hasShop);
        SetFlyoutItemIsVisible(SellerDashMenuItem, hasShop);
    }

    /// <summary>Stops listening to the session when this shell is closed.</summary>
    protected override void OnHandlerChanging(HandlerChangingEventArgs args)
    {
        base.OnHandlerChanging(args);
        if (args.NewHandler is null && _session is not null) _session.Changed -= OnSessionChanged;
    }

    // ------------------------------------------------------------------
    // Each bottom tab keeps its own stack of pages. Without this, leaving
    // Home while About/Vendors/etc. is open on it means tapping Home later
    // shows that old page instead of the Home page.
    // Fix: whenever we move to a different tab, quietly close the pages
    // left open on the tab we just left, so every tab reopens at its start.
    // ------------------------------------------------------------------
    ShellSection? _lastTab;

    protected override void OnNavigated(ShellNavigatedEventArgs args)
    {
        base.OnNavigated(args);

        var currentTab = CurrentItem?.CurrentItem;   // the tab now on screen
        var previousTab = _lastTab;
        _lastTab = currentTab;

        if (previousTab is not null && previousTab != currentTab && previousTab.Stack.Count > 1)
        {
            // The tab we left isn't visible, so this happens without any flicker.
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                try { await previousTab.Navigation.PopToRootAsync(false); }
                catch { /* nothing to close */ }
            });
        }
    }

    // The ✕ in the side menu's header: closes the menu
    void OnCloseMenuTapped(object? sender, TappedEventArgs e) => FlyoutIsPresented = false;

    // Side-menu entries for the pages above: close the menu, then open the page
    // on its tab, e.g. "//account/about" = Account tab, then About on top of it.
    async void OnMenuItemClicked(object? sender, EventArgs e)
    {
        if (sender is MenuItem { CommandParameter: string route })
        {
            FlyoutIsPresented = false;
            await GoToAsync(route);
        }
    }
}