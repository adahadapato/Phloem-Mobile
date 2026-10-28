using Phloem.Services.Api;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services;

/// <summary>
/// Couriers (and sellers delivering their own orders) on the API (<c>/api/deliveries</c>): "My deliveries", and
/// "Delivered" with a photo of where it was left.
/// </summary>
public sealed class DeliveryService(ApiClient api)
{
    /// <summary>Still to deliver (oldest first), or <paramref name="done"/> = delivered in the last 7 days.</summary>
    public Task<ApiResult<IReadOnlyList<DeliveryJobDto>>> GetMineAsync(bool done, CancellationToken ct = default) =>
        api.GetAsync<IReadOnlyList<DeliveryJobDto>>($"api/deliveries?done={(done ? "true" : "false")}", ct);

    /// <summary>Delivered, with the photo: a food order (the shop is paid) or a parcel (the sender is told).</summary>
    /// <param name="kind">Food order or parcel.</param>
    /// <param name="reference">Its order or tracking number.</param>
    /// <param name="photo">The photo (it's shrunk and its location data removed on the server).</param>
    /// <param name="fileName">The photo's file name.</param>
    /// <param name="contentType">e.g. "image/jpeg".</param>
    /// <param name="handover">How it was handed over.</param>
    /// <param name="receivedBy">Who took it (required for a neighbour).</param>
    /// <param name="note">An optional note for the customer.</param>
    /// <param name="ct">Cancellation.</param>
    public async Task<ApiResult> MarkDeliveredAsync(DeliveryKind kind, string reference, Stream photo, string fileName,
                                                    string contentType, HandoverMethod handover, string? receivedBy,
                                                    string? note, CancellationToken ct = default)
    {
        var folder = kind == DeliveryKind.Parcel ? "parcels" : "orders";
        var fields = new Dictionary<string, string> { ["handover"] = handover.ToString() };
        if (!string.IsNullOrWhiteSpace(receivedBy)) fields["receivedBy"] = receivedBy.Trim();
        if (!string.IsNullOrWhiteSpace(note)) fields["note"] = note.Trim();

        return await api.UploadAsync(HttpMethod.Post,
            $"api/deliveries/{folder}/{Uri.EscapeDataString(reference)}/delivered", "photo", photo, fileName,
            contentType, fields, ct);
    }
}
