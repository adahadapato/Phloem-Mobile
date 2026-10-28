using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>
/// The courier's photo and handover details, on an order or parcel page. Same shape as the API's
/// <c>DeliveryProofDto</c>. <see cref="PhotoUrl"/> works for about 15 minutes; reload the page for a fresh one.
/// </summary>
public sealed record DeliveryProofDto(
    string PhotoUrl,
    HandoverMethod Handover,
    string Summary,
    string? ReceivedBy,
    string? Note,
    string? CourierName,
    DateTime DeliveredAt);
