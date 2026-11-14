using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>A box line on a parcel (declared or measured). Same shape as the API's <c>ShipmentBoxDto</c>.</summary>
public sealed record ShipmentBoxDto(
    ParcelBoxKind Kind,
    decimal WeightKg,
    decimal? LengthCm,
    decimal? WidthCm,
    decimal? HeightCm,
    int Quantity,
    decimal ChargeableWeightKg);
