using Phloem.Services.Api;
using Phloem.Services.Api.Contracts.Commands;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services;

/// <summary>
/// Admin → Shipping routes on the API (<c>/api/admin/shipment-routes</c>): create a route (e.g. Ghana → UK), its
/// drop-off hub, a rate per speed, and switching it on for customers. A route is found by its two countries.
/// </summary>
public sealed class ShipmentRouteService(ApiClient api)
{
    private const string Base = "api/admin/shipment-routes";

    /// <summary>Every route, on or off, with its rates.</summary>
    public Task<ApiResult<List<AdminShipmentRouteDto>>> GetAllAsync(CancellationToken ct = default) =>
        api.GetAsync<List<AdminShipmentRouteDto>>(Base, ct);

    /// <summary>Creates a route (it starts switched off: add rates, then switch it on).</summary>
    public Task<ApiResult> CreateAsync(CreateShipmentRouteCommand command, CancellationToken ct = default) =>
        api.PostAsync(Base, command, ct);

    /// <summary>Changes where and when parcels are dropped off for a route.</summary>
    public Task<ApiResult> SetDropOffAsync(Country from, Country to, DropOffRequest body,
                                           CancellationToken ct = default) =>
        api.PutAsync($"{Route(from, to)}/drop-off", body, ct);

    /// <summary>Switches a route on (it needs at least one rate) or off.</summary>
    public Task<ApiResult> SetActiveAsync(Country from, Country to, bool isActive, CancellationToken ct = default) =>
        api.PutAsync($"{Route(from, to)}/active", new ActiveRequest(isActive), ct);

    /// <summary>Sets one speed's price and transit time on a route.</summary>
    public Task<ApiResult> SetRateAsync(Country from, Country to, DeliverySpeed speed, ShipmentRateRequest body,
                                        CancellationToken ct = default) =>
        api.PutAsync($"{Route(from, to)}/rates/{speed}", body, ct);

    /// <summary>Stops offering a speed on a route.</summary>
    public Task<ApiResult> RemoveRateAsync(Country from, Country to, DeliverySpeed speed,
                                           CancellationToken ct = default) =>
        api.DeleteAsync($"{Route(from, to)}/rates/{speed}", ct);

    /// <summary>Size divisor, fragile charge, home pickup (null = off) and destination collection hub (blank = off).</summary>
    public Task<ApiResult> SetParcelOptionsAsync(Country from, Country to, ParcelOptionsRequest body,
                                                 CancellationToken ct = default) =>
        api.PutAsync($"{Route(from, to)}/parcel-options", body, ct);

    /// <summary>The door delivery charge for one zone of the destination country (the route's currency).</summary>
    public Task<ApiResult> SetZoneChargeAsync(Country from, Country to, Guid zoneId, decimal doorDeliveryCharge,
                                              CancellationToken ct = default) =>
        api.PutAsync($"{Route(from, to)}/zones/{zoneId}", new ZoneChargeRequest(doorDeliveryCharge), ct);

    /// <summary>Removes a zone's door delivery charge (door delivery there then costs nothing extra).</summary>
    public Task<ApiResult> RemoveZoneChargeAsync(Country from, Country to, Guid zoneId, CancellationToken ct = default) =>
        api.DeleteAsync($"{Route(from, to)}/zones/{zoneId}", ct);

    private static string Route(Country from, Country to) => $"{Base}/{from}/{to}";
}
