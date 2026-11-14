namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>How one box line is charged ("counts as 12 kg"). Same shape as the API's <c>ParcelBoxChargeDto</c>.</summary>
public sealed record ParcelBoxChargeDto(
    decimal WeightKg,
    decimal? VolumeWeightKg,
    decimal ChargeableWeightKgEach,
    int Quantity,
    decimal ChargeableWeightKg,
    bool ChargedBySize);
