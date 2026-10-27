using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>
/// A country's shop delivery-fee range and the delivery times it offers (admin). Countries not set up yet aren't
/// listed. Same shape as the API's <c>DeliveryPricingDto</c>.
/// </summary>
public sealed record DeliveryPricingDto(
    Country Country,
    Currency Currency,
    decimal MinShopFee,
    decimal MaxShopFee,
    IReadOnlyList<DeliveryWindowChargeDto> Windows);

/// <summary>One delivery time a country offers, and what it adds to an order (0 = free).</summary>
public sealed record DeliveryWindowChargeDto(DeliveryWindow Window, decimal ExtraCharge);
