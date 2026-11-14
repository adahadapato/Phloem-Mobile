namespace Phloem.Services.Api.Contracts.Commands;

/// <summary>
/// Body of <c>PUT /api/admin/shipment-routes/{from}/{to}/parcel-options</c>: size divisor, fragile charge, home pickup
/// (null = off) and the destination collection hub (blank = off).
/// </summary>
public sealed record ParcelOptionsRequest(int VolumeDivisor, decimal FragileCharge, decimal? HomePickupCharge,
                                          string? CollectionHubAddress, string? CollectionHubHours);
