using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>Estimated price and transit time. Same shape as the API's <c>ShipmentQuoteDto</c>.</summary>
public sealed record ShipmentQuoteDto(
    DeliverySpeed Speed,
    decimal EstimatedWeightKg,
    decimal ChargeableWeightKg,
    decimal EstimatedPrice,
    Currency Currency,
    int MinTransitDays,
    int MaxTransitDays,
    IReadOnlyList<ParcelBoxChargeDto>? Boxes = null,
    decimal FreightCharge = 0,
    decimal FragileCharge = 0,
    decimal HomePickupCharge = 0,
    decimal DoorDeliveryCharge = 0,
    string? DeliveryZoneName = null,
    int VolumeDivisor = 5000,
    decimal BaseCharge = 0,
    decimal PricePerKg = 0);
