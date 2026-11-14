namespace Phloem.Services.Api.Contracts.Commands;

/// <summary>Body of <c>PUT /api/admin/shipment-routes/{from}/{to}/zones/{zoneId}</c>.</summary>
public sealed record ZoneChargeRequest(decimal DoorDeliveryCharge);
