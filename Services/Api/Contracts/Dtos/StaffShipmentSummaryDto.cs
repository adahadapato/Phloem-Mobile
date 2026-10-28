using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>One row in the staff / admin shipments list. Same shape as the API's <c>StaffShipmentSummaryDto</c>.</summary>
public sealed record StaffShipmentSummaryDto(
    string TrackingNumber,
    Country FromCountry,
    Country ToCountry,
    string SenderName,
    string RecipientName,
    ShipmentStatus Status,
    DeliverySpeed Speed,
    int ItemCount,
    decimal EstimatedWeightKg,
    decimal? WeighedWeightKg,
    decimal Price,
    Currency Currency,
    DateTime BookedAt,
    string? CourierName = null);
