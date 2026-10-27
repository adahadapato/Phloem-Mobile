using Phloem.Services.Api;
using Phloem.Services.Api.Contracts.Commands;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services;

/// <summary>
/// Admin → Pricing on the API (Admin role only):
/// <list type="bullet">
///   <item><description><c>/api/admin/commissions</c>: Phloem's commission in each country (on dishes only).</description></item>
///   <item><description><c>/api/admin/delivery-pricing</c>: the range shops may charge for delivery, and the
///   delivery times offered with what each adds.</description></item>
/// </list>
/// </summary>
public sealed class PricingService(ApiClient api)
{
    private const string Commissions = "api/admin/commissions";
    private const string Delivery = "api/admin/delivery-pricing";

    /// <summary>The commission in every country (IsSet = false: not chosen yet, counts as 0%).</summary>
    public Task<ApiResult<List<CommissionDto>>> GetCommissionsAsync(CancellationToken ct = default) =>
        api.GetAsync<List<CommissionDto>>(Commissions, ct);

    /// <summary>Sets a country's commission (0–50%); only orders delivered from now on use it.</summary>
    public Task<ApiResult> SetCommissionAsync(Country country, decimal ratePercent, CancellationToken ct = default) =>
        api.PutAsync($"{Commissions}/{country}", new CommissionRateRequest(ratePercent), ct);

    /// <summary>Every country's fee range and delivery times (countries not set up yet aren't listed).</summary>
    public Task<ApiResult<List<DeliveryPricingDto>>> GetDeliveryPricingAsync(CancellationToken ct = default) =>
        api.GetAsync<List<DeliveryPricingDto>>(Delivery, ct);

    /// <summary>Sets a country's shop fee range; returns how many shops had their fee moved into it (they're emailed).</summary>
    public Task<ApiResult<int>> SetShopFeeRangeAsync(Country country, decimal min, decimal max,
                                                     CancellationToken ct = default) =>
        api.PutAsync<int>($"{Delivery}/{country}", new ShopFeeRangeRequest(min, max), ct);

    /// <summary>Offers a delivery time in a country, or changes what it adds (0 = free).</summary>
    public Task<ApiResult> SetWindowChargeAsync(Country country, DeliveryWindow window, decimal extraCharge,
                                                CancellationToken ct = default) =>
        api.PutAsync($"{Delivery}/{country}/windows/{window}", new WindowChargeRequest(extraCharge), ct);

    /// <summary>Stops offering a delivery time in a country.</summary>
    public Task<ApiResult> RemoveWindowAsync(Country country, DeliveryWindow window, CancellationToken ct = default) =>
        api.DeleteAsync($"{Delivery}/{country}/windows/{window}", ct);
}
