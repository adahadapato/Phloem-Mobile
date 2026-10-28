using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>One order in the seller's lists (<c>GET /api/shops/mine/orders</c>). Same shape as the API's
/// <c>ShopOrderSummaryDto</c>.</summary>
public sealed record ShopOrderSummaryDto(
    string OrderNumber,
    string CustomerName,
    OrderStatus Status,
    DateTime? PaidAt,
    int ItemCount,
    string ItemsSummary,
    decimal Total,
    Currency Currency,
    DeliveryWindow DeliveryWindow,
    string DeliveryLabel,
    string DeliveryAddress,
    string? DeliveryRecipientName = null,
    string? DeliveryPhone = null,
    string? CourierName = null,
    bool DeliveringMyself = false);
