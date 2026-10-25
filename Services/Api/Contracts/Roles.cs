namespace Phloem.Services.Api.Contracts;

/// <summary>Role names the API puts in the sign-in token. Same values as the API's <c>Roles</c>.</summary>
public static class Roles
{
    /// <summary>Everything: approve shops, manage pricing, users and staff.</summary>
    public const string Admin = "Admin";

    /// <summary>Every signed-up user.</summary>
    public const string Customer = "Customer";

    /// <summary>Given when an admin approves the user's shop.</summary>
    public const string Seller = "Seller";

    /// <summary>Manages dish names and price ranges.</summary>
    public const string CatalogManager = "CatalogManager";

    /// <summary>Hub staff: receives, weighs and moves parcels.</summary>
    public const string ShipmentManager = "ShipmentManager";

    /// <summary>Helps customers.</summary>
    public const string SupportAgent = "SupportAgent";

    /// <summary>
    /// The roles an admin can give or take on Admin → Users (same as the API's <c>Roles.Assignable</c>). Customer is
    /// everyone's, and Seller follows the person's shop.
    /// </summary>
    public static readonly IReadOnlyList<string> Assignable = [Admin, CatalogManager, ShipmentManager, SupportAgent];

    /// <summary>A role as people read it: "CatalogManager" → "Catalog manager".</summary>
    public static string DisplayName(string role) => role switch
    {
        CatalogManager => "Catalog manager",
        ShipmentManager => "Shipment manager",
        SupportAgent => "Support agent",
        _ => role
    };

    /// <summary>What a role lets someone do, for the "give this role?" question.</summary>
    public static string Description(string role) => role switch
    {
        Admin => "Everything: shops, users, prices, shipping and staff.",
        CatalogManager => "Reviews dish-name suggestions and edits the dish list.",
        ShipmentManager => "Receives parcels at the hub and moves them along their journey.",
        SupportAgent => "Looks up orders and users, and cancels orders for customers.",
        _ => string.Empty
    };
}
