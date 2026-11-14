using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>Answer of booking a parcel: where and when to drop it off. Same shape as the API's <c>BookedShipmentDto</c>.</summary>
public sealed record BookedShipmentDto(
    string TrackingNumber,
    decimal EstimatedPrice,
    Currency Currency,
    string DropOffAddress,
    string DropOffHours,
    ParcelHandOver HandOver = ParcelHandOver.HubDropOff,
    ParcelDelivery Delivery = ParcelDelivery.ToDoor,
    string? CollectionHubAddress = null,
    string? CollectionHubHours = null);
