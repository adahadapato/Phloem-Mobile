namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>Where to collect a parcel for home pickup. Same shape as the API's <c>ShipmentPickupInputDto</c>.</summary>
public sealed record ShipmentPickupInputDto(string AddressLine, string City, string RegionOrPostcode, string? Notes);
