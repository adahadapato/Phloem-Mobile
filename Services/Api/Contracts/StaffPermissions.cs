namespace Phloem.Services.Api.Contracts;

/// <summary>
/// Staff permissions, exactly as the API's <c>Permissions</c>. Named StaffPermissions so it never clashes with
/// MAUI's own <c>Permissions</c> (device permissions such as the camera).
/// </summary>
/// <remarks>
/// The BACKEND decides who holds what and sends it with every sign-in and renewal (<c>AuthTokensDto.Permissions</c>;
/// "*" = every permission, i.e. the owners). The app only uses them to show or hide menus and buttons: every API call
/// is still checked by the API, so hiding something here is tidiness, not security.
/// </remarks>
public static class StaffPermissions
{
    /// <summary>Every permission (owners / Admin).</summary>
    public const string Everything = "*";

    /// <summary>See people, suspend and reinstate them.</summary>
    public const string UsersManage = "users.manage";

    /// <summary>Give or take job roles and countries (never Admin).</summary>
    public const string StaffManage = "staff.manage";

    /// <summary>Approve, verify and block shops; check their documents.</summary>
    public const string ShopsManage = "shops.manage";

    /// <summary>Dish names, suggestions and price ranges.</summary>
    public const string CatalogManage = "catalog.manage";

    /// <summary>Delivery fee ranges and delivery times.</summary>
    public const string PricingManage = "pricing.manage";

    /// <summary>Phloem's commission in each country.</summary>
    public const string Commission = "commission.manage";

    /// <summary>Receive and weigh parcels, move them along, pick couriers.</summary>
    public const string ParcelsManage = "parcels.manage";

    /// <summary>Routes, prices, hubs and what can be sent.</summary>
    public const string ParcelsSettings = "parcels.settings";

    /// <summary>Read and answer Contact us messages.</summary>
    public const string ContactRead = "contact.read";

    /// <summary>See the staff audit log.</summary>
    public const string AuditView = "audit.view";
}
