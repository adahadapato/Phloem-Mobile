namespace Phloem.Services.Api.Contracts.Commands;

/// <summary>Become a Seller (<c>POST /api/shops</c>). Blank email / phone = use my account's. Same shape as the API's
/// <c>OpenShopCommand</c>. <c>AgreedToSellerTerms</c> = "I agree to the Seller terms" (required).</summary>
public sealed record OpenShopCommand(string ShopName, string? BusinessEmail, string? BusinessPhone,
                                     bool AgreedToSellerTerms);
