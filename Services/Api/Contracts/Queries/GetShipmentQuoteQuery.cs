using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Queries;

/// <summary>Body of <c>POST /api/shipments/quote</c>. Same shape as the API's <c>GetShipmentQuoteQuery</c>.</summary>
public sealed record GetShipmentQuoteQuery(
    Country FromCountry,
    Country ToCountry,
    DeliverySpeed Speed,
    IReadOnlyList<ParcelBoxInputDto> Boxes,
    bool IsFragile,
    ParcelHandOver HandOver,
    ParcelDelivery Delivery,
    Guid? RecipientRegionId);
