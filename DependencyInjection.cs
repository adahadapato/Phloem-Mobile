using Microsoft.Extensions.Configuration;
using Phloem.Services;
using Phloem.Services.Api;
using Phloem.Settings;
using Phloem.ViewModels;
using Phloem.Views;

namespace Phloem;

/// <summary>
/// class DependencyInjection is a static class that provides a method to 
/// register services, view models, and pages for dependency injection in the application.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// configures the dependency injection container 
    /// by registering services, view models, and pages.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The app's settings (Settings/appsettings*.json).</param>
    public static void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        // ---- API: settings, tokens, HttpClients, ApiClient ----
        services.AddPhloemApi(configuration);

        // ---- Long lists a page at a time: sizes from appsettings "Paging" ----
        services.Configure<PagingSettings>(configuration.GetSection(PagingSettings.SectionName));
        services.AddSingleton<PagingFactory>();

        // ---- API feature services (one per API area) ----
        services.AddSingleton<AuthService>();       // /api/auth
        services.AddSingleton<AccountService>();    // /api/me
        services.AddSingleton<CountryService>();    // /api/countries (fills CountryOption.All)
        services.AddSingleton<LegalService>();      // /api/legal/{type}: Terms, Privacy, Help (kept on the phone)
        services.AddSingleton<ParcelRulesService>(); // /api/parcel-rules: what can be sent (kept on the phone) + admin
        services.AddSingleton<DishService>();       // /api/dishes
        services.AddSingleton<ShopService>();       // /api/shops
        services.AddSingleton<OrderService>();      // /api/orders
        services.AddSingleton<ShipmentService>();   // /api/shipments, /api/staff/shipments
        services.AddSingleton<ShipmentRouteService>(); // /api/admin/shipment-routes
        services.AddSingleton<SellerService>();     // /api/shops/mine, /api/shops/mine/orders
        services.AddSingleton<AdminService>();      // /api/admin/shops, /api/admin/users, /api/admin/dish-catalog

        // ---- Services (singletons so mock "database" is shared across pages) ----
        services.AddSingleton<MockDataService>();
        services.AddSingleton<CartService>();
        services.AddSingleton<SessionService>();
        services.AddSingleton<OrderHistoryService>();
        services.AddSingleton<DeliveryAddressService>();
        services.AddSingleton<DishCatalogService>();


        // ---- ViewModels ----
        services.AddTransient<HomeViewModel>();
        services.AddTransient<RegisterViewModel>();
        services.AddTransient<SellerSetupViewModel>();
        services.AddTransient<FoodListingViewModel>();
        services.AddTransient<SellerDashboardViewModel>();
        services.AddTransient<InternationalDeliveryViewModel>();
        services.AddTransient<CheckoutViewModel>();
        services.AddTransient<VendorsListViewModel>();
        services.AddTransient<LoginViewModel>();
        services.AddTransient<AdminViewModel>();
        services.AddTransient<OrdersViewModel>();
        services.AddTransient<DeliveryAddressViewModel>();
        services.AddTransient<OrderDetailsViewModel>();
        services.AddTransient<AccountViewModel>();
        services.AddTransient<DishEditorViewModel>();
        services.AddTransient<EditProfileViewModel>();
        services.AddTransient<ChangePasswordViewModel>();
        services.AddTransient<NotificationSettingsViewModel>();
        services.AddTransient<ShopSettingsViewModel>();
        services.AddTransient<MyShipmentsViewModel>();
        services.AddTransient<ShipmentDetailsViewModel>();
        services.AddTransient<AdminShipmentsViewModel>();
        services.AddTransient<AdminShipmentRoutesViewModel>();
        services.AddTransient<AdminShopDetailsViewModel>();
        services.AddTransient<ShopOrderDetailsViewModel>();
        services.AddTransient<AdminDishCatalogViewModel>();
        services.AddTransient<LegalViewModel>();
        services.AddTransient<AdminParcelRulesViewModel>();




        // ---- Pages ----
        services.AddTransient<StartupPage>();       // first page: loads countries + sign-in, then opens AppShell
        services.AddTransient<AboutPage>();
        services.AddTransient<LegalPage>();
        services.AddTransient<AdminParcelRulesPage>();
        services.AddTransient<HomePage>();
        services.AddTransient<RegisterPage>();
        services.AddTransient<SellerSetupPage>();
        services.AddTransient<FoodListingPage>();
        services.AddTransient<SellerDashboardPage>();
        services.AddTransient<InternationalDeliveryPage>();
        services.AddTransient<CheckoutPage>();
        services.AddTransient<VendorsListPage>();
        services.AddTransient<LoginPage>();
        services.AddTransient<AdminPage>();
        services.AddTransient<OrdersPage>();
        services.AddTransient<DeliveryAddressPage>();
        services.AddTransient<OrderDetailsPage>();
        services.AddTransient<AccountPage>();
        services.AddTransient<DishEditorPage>();
        services.AddTransient<EditProfilePage>();
        services.AddTransient<ChangePasswordPage>();
        services.AddTransient<NotificationSettingsPage>();
        services.AddTransient<ShopSettingsPage>();
        services.AddTransient<MyShipmentsPage>();
        services.AddTransient<ShipmentDetailsPage>();
        services.AddTransient<AdminShipmentsPage>();
        services.AddTransient<AdminShipmentRoutesPage>();
        services.AddTransient<AdminShopDetailsPage>();
        services.AddTransient<ShopOrderDetailsPage>();
        services.AddTransient<AdminDishCatalogPage>();
    }
}
