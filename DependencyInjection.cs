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

        // ---- Sign in with Face ID / fingerprint: the phone's own check, one class per platform ----
#if ANDROID
        services.AddSingleton<Phloem.Services.Biometrics.IBiometricService, Phloem.Services.Biometrics.AndroidBiometricService>();
#elif IOS
        services.AddSingleton<Phloem.Services.Biometrics.IBiometricService, Phloem.Services.Biometrics.IosBiometricService>();
#else
        services.AddSingleton<Phloem.Services.Biometrics.IBiometricService, Phloem.Services.Biometrics.NoBiometricService>();
#endif
        services.AddSingleton<Phloem.Services.Biometrics.BiometricSettings>();   // on/off, saved on this phone
        services.AddSingleton<Phloem.Services.Biometrics.BiometricSignIn>();     // start-up, Log in, My account
        services.AddSingleton<SessionLock>();

        // ---- Voice search (🎤) and dish-name pronunciation (🔊) ----
#if ANDROID
        services.AddSingleton<Phloem.Services.Voice.IVoiceSearchService, Phloem.Services.Voice.AndroidVoiceSearchService>();
#elif IOS
        services.AddSingleton<Phloem.Services.Voice.IVoiceSearchService, Phloem.Services.Voice.IosVoiceSearchService>();
#else
        services.AddSingleton<Phloem.Services.Voice.IVoiceSearchService, Phloem.Services.Voice.NoVoiceSearchService>();
#endif
        services.AddSingleton<Phloem.Services.Voice.VoiceSearch>();
        services.AddSingleton<Phloem.Services.Pronunciation.PronunciationPlayer>();
        services.AddTransient<Phloem.Services.Pronunciation.PronunciationRecorder>();   // sellers / staff: sign in again after too long in the background
        services.AddSingleton<AccountService>();    // /api/me
        services.AddSingleton<CountryService>();    // /api/countries (fills CountryOption.All)
        services.AddSingleton<LegalService>();      // /api/legal/{type}: Terms, Privacy, Help (kept on the phone)
        services.AddSingleton<ParcelRulesService>(); // /api/parcel-rules: what can be sent (kept on the phone) + admin
        services.AddSingleton<DishService>();       // /api/dishes
        services.AddSingleton<ShopService>();       // /api/shops
        services.AddSingleton<OrderService>();      // /api/orders
        services.AddSingleton<ShipmentService>();   // /api/shipments, /api/staff/shipments
        services.AddSingleton<ReviewService>();     // /api/orders/{n}/review, /api/shipments/{t}/review
        services.AddSingleton<LocationService>();   // /api/locations: states, LGAs, towns (kept while the app runs)
        services.AddSingleton<ShipmentRouteService>(); // /api/admin/shipment-routes
        services.AddSingleton<PricingService>();
        services.AddSingleton<ContactService>();
        services.AddSingleton<DeliveryService>();
        services.AddSingleton<ScreenAwakeService>();
        services.AddSingleton<TermsPromptService>(); // "We've updated our Terms" / Privacy notice     // /api/contact (Contact us)
        services.AddSingleton<SuggestionService>(); // /api/me/suggestions, /api/dishes/popular (Home)    // /api/admin/commissions, /api/admin/delivery-pricing
        services.AddSingleton<SellerService>();     // /api/shops/mine, /api/shops/mine/orders
        services.AddSingleton<AdminService>();      // /api/admin/shops, /api/admin/users, /api/admin/dish-catalog
        services.AddSingleton<StaffService>();      // /api/admin/staff (read-only in the app)

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
        services.AddTransient<AdminDishPronunciationViewModel>();
        services.AddTransient<LegalViewModel>();
        services.AddTransient<AdminParcelRulesViewModel>();
        services.AddTransient<AdminStaffViewModel>();
        services.AddTransient<AdminPricingViewModel>();
        services.AddTransient<ContactViewModel>();
        services.AddTransient<MyDeliveriesViewModel>();
        services.AddTransient<DeliveryProofViewModel>();
        services.AddTransient<TermsUpdateViewModel>();




        // ---- Pages ----
        services.AddTransient<StartupPage>();       // first page: loads countries + sign-in, then opens AppShell
        services.AddTransient<AboutPage>();
        services.AddTransient<LegalPage>();
        services.AddTransient<AdminParcelRulesPage>();
        services.AddTransient<AdminStaffPage>();
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
        services.AddTransient<AdminDishPronunciationPage>();
        services.AddTransient<AdminPricingPage>();
        services.AddTransient<ContactPage>();
        services.AddTransient<MyDeliveriesPage>();
        services.AddTransient<DeliveryProofPage>();
        services.AddTransient<TermsUpdatePage>();
    }
}
