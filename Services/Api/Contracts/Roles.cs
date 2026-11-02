namespace Phloem.Services.Api.Contracts;

/// <summary>Role names the API puts in the sign-in token. Same values as the API's <c>Roles</c>.</summary>
/// <remarks>
/// The three older staff roles keep their stored names (SupportAgent, CatalogManager, ShipmentManager) and are shown
/// as Support, Shops &amp; catalogue and Parcel hub. What a person may DO comes from their permissions
/// (<see cref="StaffPermissions"/>), which the backend sends with the sign-in, not from these names.
/// </remarks>
public static class Roles
{
    /// <summary>The owners: everything, in every country.</summary>
    public const string Admin = "Admin";

    /// <summary>Every signed-up user.</summary>
    public const string Customer = "Customer";

    /// <summary>Given when an admin approves the user's shop.</summary>
    public const string Seller = "Seller";

    /// <summary>Job role: runs one country.</summary>
    public const string CountryManager = "CountryManager";

    /// <summary>Job role "Shops &amp; catalogue": approves shops, edits the dish list.</summary>
    public const string CatalogManager = "CatalogManager";

    /// <summary>Job role "Parcel hub": receives, weighs and moves parcels.</summary>
    public const string ShipmentManager = "ShipmentManager";

    /// <summary>Job role "Support": Contact messages, orders, emails, chats and problem reports.</summary>
    public const string SupportAgent = "SupportAgent";

    /// <summary>Job role: payments, refunds, withdrawals and commission.</summary>
    public const string Finance = "Finance";

    /// <summary>Job role: search engine text and AI prompts.</summary>
    public const string Content = "Content";

    /// <summary>Job role: settings and system health.</summary>
    public const string Technical = "Technical";

    /// <summary>Delivers food orders and parcels, with a photo as proof of delivery.</summary>
    public const string Courier = "Courier";

    /// <summary>The staff job roles (not Admin), in the usual order.</summary>
    public static readonly IReadOnlyList<string> JobRoles =
        [CountryManager, SupportAgent, CatalogManager, ShipmentManager, Finance, Content, Technical];

    /// <summary>
    /// The roles that can be given or taken on Admin → Users (same as the API's <c>Roles.Assignable</c>). Customer is
    /// everyone's, and Seller follows the person's shop. Only owners can give or take Admin.
    /// </summary>
    public static readonly IReadOnlyList<string> Assignable = [Admin, .. JobRoles, Courier];

    /// <summary>A role as people read it: "ShipmentManager" → "Parcel hub".</summary>
    public static string DisplayName(string role) => role switch
    {
        CountryManager => "Country manager",
        CatalogManager => "Shops & catalogue",
        ShipmentManager => "Parcel hub",
        SupportAgent => "Support",
        _ => role
    };

    /// <summary>What a role lets someone do, for the "give this role?" question.</summary>
    public static string Description(string role) => role switch
    {
        Admin => "Owners: everything, in every country.",
        CountryManager => "Runs one country: shops, dish list, pricing, orders and parcels.",
        CatalogManager => "Approves shops and their documents, and edits the dish list.",
        ShipmentManager => "Receives parcels at the hub and moves them along their journey.",
        SupportAgent => "Contact messages, orders, team emails, chats and problem reports.",
        Finance => "Payments, refunds, seller withdrawals and commission.",
        Content => "Search engine text and AI prompts.",
        Technical => "Settings and system health; no customer data.",
        Courier => "Delivers food orders and parcels, and takes a photo when handing them over.",
        _ => string.Empty
    };
}
