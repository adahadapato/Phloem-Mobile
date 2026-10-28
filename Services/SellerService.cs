using Phloem.Services.Api;
using Phloem.Services.Api.Contracts;
using Phloem.Services.Api.Contracts.Commands;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services;

/// <summary>
/// The signed-in seller's shop on the API: open it, its details, pictures, documents, delivery fee, order cut-off
/// (<c>/api/shops/mine</c>), its orders (<c>/api/shops/mine/orders</c>) and its dishes (<c>/api/shops/mine/dishes</c>).
/// "mine" comes from the sign-in, never from the app.
/// </summary>
public sealed class SellerService(ApiClient api)
{
    private const string Mine = "api/shops/mine";

    // ───────────────────────────── 1. My shop ─────────────────────────────

    /// <summary>Become a Seller: opens my shop (waits for admin approval). Needs personal details first.</summary>
    public Task<ApiResult<Guid>> OpenShopAsync(OpenShopCommand command, CancellationToken ct = default) =>
        api.PostAsync<Guid>("api/shops", command, ct);

    /// <summary>My shop (404 when the user hasn't opened one).</summary>
    public Task<ApiResult<MyShopDto>> GetMyShopAsync(CancellationToken ct = default) =>
        api.GetAsync<MyShopDto>(Mine, ct);

    /// <summary>Shop settings → name, business email and phone.</summary>
    public Task<ApiResult> UpdateDetailsAsync(UpdateShopDetailsCommand command, CancellationToken ct = default) =>
        api.PutAsync(Mine, command, ct);

    /// <summary>Uploads my shop's logo; returns its URL.</summary>
    public Task<ApiResult<string>> ChangeLogoAsync(Stream picture, string fileName, string contentType,
                                                   CancellationToken ct = default) =>
        api.UploadAsync<string>(HttpMethod.Put, $"{Mine}/logo", "logo", picture, fileName, contentType, ct: ct);

    public Task<ApiResult> RemoveLogoAsync(CancellationToken ct = default) =>
        api.DeleteAsync($"{Mine}/logo", ct);

    /// <summary>Uploads my shop's cover photo (banner); returns its URL.</summary>
    public Task<ApiResult<string>> ChangeCoverPhotoAsync(Stream picture, string fileName, string contentType,
                                                         CancellationToken ct = default) =>
        api.UploadAsync<string>(HttpMethod.Put, $"{Mine}/cover-photo", "coverPhoto", picture, fileName, contentType,
                                ct: ct);

    public Task<ApiResult> RemoveCoverPhotoAsync(CancellationToken ct = default) =>
        api.DeleteAsync($"{Mine}/cover-photo", ct);

    /// <summary>Uploads a document for review (PDF, JPEG or PNG, max 10 MB); replaces one of the same type.</summary>
    public Task<ApiResult<Guid>> UploadDocumentAsync(VendorDocumentType type, DateOnly? expiresOn, Stream file,
                                                     string fileName, string contentType,
                                                     CancellationToken ct = default)
    {
        var fields = new Dictionary<string, string> { ["type"] = type.ToString() };
        if (expiresOn is { } date) fields["expiresOn"] = date.ToString("yyyy-MM-dd");
        return api.UploadAsync<Guid>(HttpMethod.Post, $"{Mine}/documents", "file", file, fileName, contentType,
                                     fields, ct);
    }

    /// <summary>My delivery fee and the range allowed in my country.</summary>
    public Task<ApiResult<MyDeliveryFeeDto>> GetDeliveryFeeAsync(CancellationToken ct = default) =>
        api.GetAsync<MyDeliveryFeeDto>($"{Mine}/delivery-fee", ct);

    public Task<ApiResult> SetDeliveryFeeAsync(decimal fee, CancellationToken ct = default) =>
        api.PutAsync($"{Mine}/delivery-fee", new SetMyDeliveryFeeCommand(fee), ct);

    /// <summary>When orders for today close (my shop's local time); after it buyers can only pick Tomorrow.</summary>
    public Task<ApiResult> SetOrderCutoffAsync(TimeOnly cutoff, CancellationToken ct = default) =>
        api.PutAsync($"{Mine}/order-cutoff", new SetMyOrderCutoffCommand(cutoff), ct);

    /// <summary>Orders per status and this week's daily sales.</summary>
    public Task<ApiResult<ShopOrderStatsDto>> GetStatsAsync(CancellationToken ct = default) =>
        api.GetAsync<ShopOrderStatsDto>("api/shops/mine/orders/stats", ct);

    // ───────────────────────────── 2. Orders ─────────────────────────────

    /// <summary>My shop's orders with one status, newest first (Paid = waiting to be accepted); one page.</summary>
    public Task<ApiResult<PagedResult<ShopOrderSummaryDto>>> GetOrdersAsync(OrderStatus status, int page, int pageSize,
                                                                            CancellationToken ct = default) =>
        api.GetAsync<PagedResult<ShopOrderSummaryDto>>(
            $"api/shops/mine/orders?status={status}&page={page}&pageSize={pageSize}", ct);

    /// <summary>One order with its dishes: what the customer ordered, before accepting it.</summary>
    public Task<ApiResult<OrderDetailsDto>> GetOrderAsync(string orderNumber, CancellationToken ct = default) =>
        api.GetAsync<OrderDetailsDto>($"api/orders/{Uri.EscapeDataString(orderNumber)}", ct);

    /// <summary>Accept a waiting order and start preparing it.</summary>
    public Task<ApiResult> AcceptAsync(string orderNumber, CancellationToken ct = default) =>
        api.PostAsync($"{OrderPath(orderNumber)}/accept", null, ct);

    /// <summary>Decline a waiting order: it's cancelled and refunded, and the customer is emailed the reason.</summary>
    public Task<ApiResult> DeclineAsync(string orderNumber, string reason, CancellationToken ct = default) =>
        api.PostAsync($"{OrderPath(orderNumber)}/decline", new DeclineRequest(reason), ct);

    /// <summary>
    /// The order has left the kitchen with a courier (null = I'll deliver it myself). The courier marks it delivered
    /// with a photo (<see cref="DeliveryService"/>), and then the earnings go to my balance.
    /// </summary>
    public Task<ApiResult> SendOutAsync(string orderNumber, Guid? courierId, CancellationToken ct = default) =>
        api.PostAsync($"{OrderPath(orderNumber)}/send-out", new CourierRequest(courierId), ct);

    /// <summary>Gives an order that's out for delivery to another courier (null = I'll deliver it myself).</summary>
    public Task<ApiResult> ChangeCourierAsync(string orderNumber, Guid? courierId, CancellationToken ct = default) =>
        api.PutAsync($"{OrderPath(orderNumber)}/courier", new CourierRequest(courierId), ct);

    /// <summary>Who can deliver my orders: me first ("I'll deliver it myself"), then couriers in my country.</summary>
    public Task<ApiResult<IReadOnlyList<CourierOptionDto>>> GetCouriersAsync(CancellationToken ct = default) =>
        api.GetAsync<IReadOnlyList<CourierOptionDto>>($"{Mine}/couriers", ct);

    // ───────────────────────────── 3. My dishes ─────────────────────────────

    /// <summary>My dishes (those needing a new price first).</summary>
    public Task<ApiResult<MyDishesDto>> GetMyDishesAsync(CancellationToken ct = default) =>
        api.GetAsync<MyDishesDto>($"{Mine}/dishes", ct);

    /// <summary>Adds a dish; returns its Id (add photos afterwards).</summary>
    public Task<ApiResult<Guid>> CreateDishAsync(CreateDishCommand command, CancellationToken ct = default) =>
        api.PostAsync<Guid>($"{Mine}/dishes", command, ct);

    public Task<ApiResult> UpdateDishAsync(Guid dishId, DishRequest body, CancellationToken ct = default) =>
        api.PutAsync($"{Mine}/dishes/{dishId}", body, ct);

    /// <summary>A new price only (the dashboard's − / + buttons).</summary>
    public Task<ApiResult> ChangeDishPriceAsync(Guid dishId, decimal price, CancellationToken ct = default) =>
        api.PutAsync($"{Mine}/dishes/{dishId}/price", new PriceRequest(price), ct);

    /// <summary>Deletes a dish (past orders still show it).</summary>
    public Task<ApiResult> DeleteDishAsync(Guid dishId, CancellationToken ct = default) =>
        api.DeleteAsync($"{Mine}/dishes/{dishId}", ct);

    /// <summary>Adds a photo (up to 5 per dish, max 10 MB).</summary>
    public Task<ApiResult<DishPhotoDto>> AddDishPhotoAsync(Guid dishId, Stream photo, string fileName, string contentType,
                                                          CancellationToken ct = default) =>
        api.UploadAsync<DishPhotoDto>(HttpMethod.Post, $"{Mine}/dishes/{dishId}/photos", "photo", photo, fileName,
                                      contentType, ct: ct);

    public Task<ApiResult> RemoveDishPhotoAsync(Guid dishId, Guid photoId, CancellationToken ct = default) =>
        api.DeleteAsync($"{Mine}/dishes/{dishId}/photos/{photoId}", ct);

    private static string OrderPath(string orderNumber) =>
        $"api/shops/mine/orders/{Uri.EscapeDataString(orderNumber)}";
}
