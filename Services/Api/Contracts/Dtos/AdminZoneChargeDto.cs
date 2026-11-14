namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>
/// A destination zone and the route's door delivery charge (null = none set). Same shape as the API's
/// <c>AdminZoneChargeDto</c>.
/// </summary>
public sealed record AdminZoneChargeDto(Guid ZoneId, string ZoneName, decimal? DoorDeliveryCharge);
