namespace Phloem.Services.Api.Contracts.Commands;

/// <summary>Body of "set the shop delivery-fee range" (the country is in the URL). Same as the API's <c>ShopFeeRangeRequest</c>.</summary>
public sealed record ShopFeeRangeRequest(decimal MinShopFee, decimal MaxShopFee);
