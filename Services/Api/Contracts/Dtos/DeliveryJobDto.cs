using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>One card in "My deliveries" (<c>GET /api/deliveries</c>). Same shape as the API's <c>DeliveryJobDto</c>.</summary>
public sealed record DeliveryJobDto(
    DeliveryKind Kind,
    string Reference,
    string CollectFrom,
    string? CollectPhone,
    string RecipientName,
    string? RecipientPhone,
    string Address,
    Country Country,
    string ItemsSummary,
    DeliveryWindow? DeliveryWindow,
    DateTime? AssignedAt,
    DateTime? DeliveredAt,
    string? HandoverSummary);
