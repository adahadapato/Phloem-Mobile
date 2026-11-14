using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>
/// A route parcels can be sent on (switched on by an admin), with its drop-off hub and speeds.
/// Same shape as the API's <c>ShipmentRouteOptionDto</c>.
/// </summary>
public sealed record ShipmentRouteOptionDto(
    Country FromCountry,
    Country ToCountry,
    Currency Currency,
    string DropOffAddress,
    string DropOffHours,
    IReadOnlyList<ShipmentSpeedOptionDto> Speeds,
    int VolumeDivisor = 5000,
    decimal FragileCharge = 0,
    decimal? HomePickupCharge = null,
    string? CollectionHubAddress = null,
    string? CollectionHubHours = null,
    bool HasLocationList = false);
