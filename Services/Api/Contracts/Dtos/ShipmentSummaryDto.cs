using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>One card in "My shipments". Same shape as the API's <c>ShipmentSummaryDto</c>.</summary>
public sealed record ShipmentSummaryDto(
    string TrackingNumber,
    Country FromCountry,
    Country ToCountry,
    string RecipientName,
    ShipmentStatus Status,
    DeliverySpeed Speed,
    int ItemCount,
    decimal Price,
    bool IsPriceFinal,
    Currency Currency,
    DateTime BookedAt,
    bool CanPay,
    bool CanCancel,
    ParcelDelivery Delivery = ParcelDelivery.ToDoor,
    bool IsReadyToCollect = false,
    int BoxCount = 1);
