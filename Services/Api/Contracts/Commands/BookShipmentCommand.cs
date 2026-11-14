using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Commands;

/// <summary>Body of <c>POST /api/shipments</c>. Same shape as the API's <c>BookShipmentCommand</c>.</summary>
/// <remarks>
/// The app sends <see cref="Boxes"/>; <see cref="EstimatedWeightKg"/> stays null (older app versions sent one weight).
/// The recipient's address line may be blank when they collect from our hub. Where the destination has lists
/// (Nigeria), <see cref="RecipientRegionId"/> + <see cref="RecipientDistrictId"/> are required.
/// </remarks>
public sealed record BookShipmentCommand(
    Country FromCountry,
    Country ToCountry,
    string SenderPhone,
    string RecipientName,
    string RecipientPhone,
    string RecipientAddressLine,
    string RecipientCity,
    string RecipientRegionOrPostcode,
    IReadOnlyList<ShipmentItemInputDto> Items,
    DeliverySpeed Speed,
    decimal? EstimatedWeightKg,
    bool ConfirmedNoProhibitedItems,
    bool AcceptedTerms,
    IReadOnlyList<ParcelBoxInputDto> Boxes,
    bool IsFragile,
    ParcelHandOver HandOver,
    ShipmentPickupInputDto? Pickup,
    ParcelDelivery Delivery,
    Guid? RecipientRegionId,
    Guid? RecipientDistrictId,
    Guid? RecipientTownId);
