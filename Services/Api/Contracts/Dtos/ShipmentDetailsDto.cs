using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>One parcel in full, with its timeline. Same shape as the API's <c>ShipmentDetailsDto</c>.</summary>
public sealed record ShipmentDetailsDto(
    string TrackingNumber,
    Country FromCountry,
    Country ToCountry,
    ShipmentStatus Status,
    DeliverySpeed Speed,
    string SenderName,
    string SenderPhone,
    string RecipientName,
    string RecipientPhone,
    string RecipientAddressLine,
    string RecipientCity,
    string RecipientRegionOrPostcode,
    IReadOnlyList<ShipmentItemDto> Items,
    decimal TotalDeclaredValue,
    decimal EstimatedWeightKg,
    decimal? WeighedWeightKg,
    decimal EstimatedPrice,
    decimal? FinalPrice,
    Currency Currency,
    string? DropOffAddress,
    string? DropOffHours,
    DateOnly? EstimatedArrivalFrom,
    DateOnly? EstimatedArrivalTo,
    DateTime? DeliveredAt,
    IReadOnlyList<ShipmentEventDto> Timeline,
    bool CanCancel,
    bool CanPay,
    bool NeedsRefund,
    string? CourierName = null,
    DeliveryProofDto? DeliveryProof = null);
