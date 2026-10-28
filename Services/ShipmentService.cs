using Phloem.Services.Api;
using Phloem.Services.Api.Contracts;
using Phloem.Services.Api.Contracts.Commands;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;
using Phloem.Services.Api.Contracts.Queries;

namespace Phloem.Services;

/// <summary>
/// Parcels ("Send Food to UK") on the API: routes and quotes, booking, my shipments, paying and cancelling, and the
/// staff side (every parcel, received at the hub, next step). Screens never call the API directly.
/// </summary>
public sealed class ShipmentService(ApiClient api)
{
    // ───────────────────────────── 1. Anyone ─────────────────────────────

    /// <summary>Where parcels can be sent (routes an admin has switched on).</summary>
    public Task<ApiResult<List<ShipmentRouteOptionDto>>> GetRoutesAsync(CancellationToken ct = default) =>
        api.GetAsync<List<ShipmentRouteOptionDto>>("api/shipments/routes", ct);

    /// <summary>Estimated price and transit time (nothing is saved).</summary>
    public Task<ApiResult<ShipmentQuoteDto>> QuoteAsync(GetShipmentQuoteQuery query, CancellationToken ct = default) =>
        api.PostAsync<ShipmentQuoteDto>("api/shipments/quote", query, ct);

    // ───────────────────────────── 2. Senders ─────────────────────────────

    /// <summary>Books a parcel: the tracking number, estimated price and where to drop it off.</summary>
    public Task<ApiResult<BookedShipmentDto>> BookAsync(BookShipmentCommand command, CancellationToken ct = default) =>
        api.PostAsync<BookedShipmentDto>("api/shipments", command, ct);

    /// <summary>My parcels, newest first; one page.</summary>
    public Task<ApiResult<PagedResult<ShipmentSummaryDto>>> GetMineAsync(int page, int pageSize,
                                                                         CancellationToken ct = default) =>
        api.GetAsync<PagedResult<ShipmentSummaryDto>>($"api/shipments?page={page}&pageSize={pageSize}", ct);

    /// <summary>One of my parcels with its timeline.</summary>
    public Task<ApiResult<ShipmentDetailsDto>> GetMineAsync(string trackingNumber, CancellationToken ct = default) =>
        api.GetAsync<ShipmentDetailsDto>($"api/shipments/{Uri.EscapeDataString(trackingNumber)}", ct);

    /// <summary>Cancels one of my parcels (before it leaves).</summary>
    public Task<ApiResult> CancelAsync(string trackingNumber, CancellationToken ct = default) =>
        api.PostAsync($"api/shipments/{Uri.EscapeDataString(trackingNumber)}/cancel", null, ct);

    /// <summary>
    /// TEST ONLY: pays a weighed parcel without money moving, until real payments are connected.
    /// The API answers 404 once test payments are switched off.
    /// </summary>
    public Task<ApiResult<ShipmentDetailsDto>> PayWithTestPaymentAsync(string trackingNumber,
                                                                      CancellationToken ct = default) =>
        api.PostAsync<ShipmentDetailsDto>($"api/shipments/{Uri.EscapeDataString(trackingNumber)}/test-payment",
                                          null, ct);

    // ───────────────────────────── 3. Staff (ShipmentManager or Admin) ─────────────────────────────

    /// <summary>Every parcel, newest first; optionally one status; one page.</summary>
    public Task<ApiResult<PagedResult<StaffShipmentSummaryDto>>> GetAllAsync(ShipmentStatus? status, int page,
                                                                            int pageSize,
                                                                            CancellationToken ct = default)
    {
        var path = $"api/staff/shipments?page={page}&pageSize={pageSize}";
        if (status is { } s) path += $"&status={s}";
        return api.GetAsync<PagedResult<StaffShipmentSummaryDto>>(path, ct);
    }

    /// <summary>Any parcel with its timeline (and staff notes).</summary>
    public Task<ApiResult<ShipmentDetailsDto>> GetAnyAsync(string trackingNumber, CancellationToken ct = default) =>
        api.GetAsync<ShipmentDetailsDto>($"api/staff/shipments/{Uri.EscapeDataString(trackingNumber)}", ct);

    /// <summary>Dropped off and weighed at the hub: sets the final price (returned).</summary>
    public Task<ApiResult<decimal>> ReceiveAtHubAsync(string trackingNumber, decimal weighedWeightKg,
                                                      CancellationToken ct = default) =>
        api.PostAsync<decimal>($"api/staff/shipments/{Uri.EscapeDataString(trackingNumber)}/receive",
                               new ReceiveRequest(weighedWeightKg), ct);

    /// <summary>Moves a paid parcel to its next step: in transit, then arrived. (Out for delivery needs a courier:
    /// <see cref="SendOutForDeliveryAsync"/>; the courier marks it delivered with a photo.)</summary>
    public Task<ApiResult> UpdateProgressAsync(string trackingNumber, ShipmentStatus newStatus, string? note,
                                               CancellationToken ct = default) =>
        api.PostAsync($"api/staff/shipments/{Uri.EscapeDataString(trackingNumber)}/progress",
                      new ProgressRequest(newStatus, string.IsNullOrWhiteSpace(note) ? null : note.Trim()), ct);

    /// <summary>Gives an arrived parcel to a courier in the destination country (they're emailed).</summary>
    public Task<ApiResult> SendOutForDeliveryAsync(string trackingNumber, Guid courierId, string? note,
                                                   CancellationToken ct = default) =>
        api.PostAsync($"api/staff/shipments/{Uri.EscapeDataString(trackingNumber)}/out-for-delivery",
                      new OutForDeliveryRequest(courierId, string.IsNullOrWhiteSpace(note) ? null : note.Trim()), ct);

    /// <summary>Gives a parcel that's out for delivery to another courier.</summary>
    public Task<ApiResult> ChangeCourierAsync(string trackingNumber, Guid courierId, CancellationToken ct = default) =>
        api.PutAsync($"api/staff/shipments/{Uri.EscapeDataString(trackingNumber)}/courier",
                     new ChangeCourierRequest(courierId), ct);

    /// <summary>The couriers who deliver in a country, by name.</summary>
    public Task<ApiResult<IReadOnlyList<CourierOptionDto>>> GetCouriersAsync(Country country,
                                                                             CancellationToken ct = default) =>
        api.GetAsync<IReadOnlyList<CourierOptionDto>>($"api/staff/couriers?country={country}", ct);
}
