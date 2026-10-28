using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>One order with its dishes (<c>GET /api/orders/{orderNumber}</c>). Same shape as the API's <c>OrderDetailsDto</c>.</summary>
public sealed record OrderDetailsDto(
    string OrderNumber,
    string ShopName,
    string CustomerName,
    OrderStatus Status,
    IReadOnlyList<OrderLineDto> Lines,
    decimal Subtotal,
    decimal DeliveryFee,
    decimal Total,
    Currency Currency,
    DeliveryWindow DeliveryWindow,
    string DeliveryLabel,
    string DeliveryAddress,
    DateTime PlacedAt,
    DateTime? PaidAt,
    DateTime? DeliveredAt,
    DateTime? CancelledAt,
    bool CanCancel,
    int? CancelSecondsLeft,
    bool NeedsRefund,
    bool ViewedBySeller,
    bool DeclinedByShop,
    string? DeclineReason,
    string? DeliveryRecipientName = null,
    string? DeliveryPhone = null,
    string? CourierName = null,
    DeliveryProofDto? DeliveryProof = null,
    bool CanMarkDelivered = false);
