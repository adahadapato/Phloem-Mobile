using Phloem.Services.Api;
using Phloem.Services.Api.Contracts;
using Phloem.Services.Api.Contracts.Commands;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services;

/// <summary>
/// The admin dashboard on the API (Admin role only): shops (approve, reject, ✓ badge, block, documents), dish name
/// suggestions, and users (list, suspend, lift a suspension). Parcels and routes have their own services.
/// </summary>
public sealed class AdminService(ApiClient api)
{
    private const string Shops = "api/admin/shops";
    private const string Users = "api/admin/users";
    private const string DishNames = "api/admin/dish-catalog";

    // ───────────────────────────── 1. Shops ─────────────────────────────

    /// <summary>How many shops are in each status (the stat tiles).</summary>
    public Task<ApiResult<AdminShopStatsDto>> GetShopStatsAsync(CancellationToken ct = default) =>
        api.GetAsync<AdminShopStatsDto>($"{Shops}/stats", ct);

    /// <summary>Shops waiting for approval, oldest first.</summary>
    public Task<ApiResult<List<AdminShopDto>>> GetPendingShopsAsync(CancellationToken ct = default) =>
        api.GetAsync<List<AdminShopDto>>($"{Shops}/pending", ct);

    /// <summary>All shops (any status), verified then best rated; one page.</summary>
    public Task<ApiResult<PagedResult<AdminShopDto>>> GetShopsAsync(string? search, int page, int pageSize,
                                                                   CancellationToken ct = default)
    {
        var path = $"{Shops}?page={page}&pageSize={pageSize}";
        if (!string.IsNullOrWhiteSpace(search)) path += $"&search={Uri.EscapeDataString(search.Trim())}";
        return api.GetAsync<PagedResult<AdminShopDto>>(path, ct);
    }

    /// <summary>One shop with its documents.</summary>
    public Task<ApiResult<AdminShopDetailsDto>> GetShopAsync(Guid shopId, CancellationToken ct = default) =>
        api.GetAsync<AdminShopDetailsDto>($"{Shops}/{shopId}", ct);

    /// <summary>A 10-minute link to open a private document in the browser.</summary>
    public Task<ApiResult<string>> GetDocumentLinkAsync(Guid shopId, Guid documentId, CancellationToken ct = default) =>
        api.GetAsync<string>($"{Shops}/{shopId}/documents/{documentId}/link", ct);

    public Task<ApiResult> ApproveShopAsync(Guid shopId, CancellationToken ct = default) =>
        api.PostAsync($"{Shops}/{shopId}/approve", null, ct);

    public Task<ApiResult> RejectShopAsync(Guid shopId, string reason, CancellationToken ct = default) =>
        api.PostAsync($"{Shops}/{shopId}/reject", new ReasonRequest(reason), ct);

    /// <summary>Accept or reject a document (a reason is needed to reject).</summary>
    public Task<ApiResult> ReviewDocumentAsync(Guid shopId, Guid documentId, bool accept, string? reason,
                                               CancellationToken ct = default) =>
        api.PostAsync($"{Shops}/{shopId}/documents/{documentId}/review", new ReviewRequest(accept, reason), ct);

    /// <summary>Give or remove the ✓ badge.</summary>
    public Task<ApiResult> SetVerifiedAsync(Guid shopId, bool verified, CancellationToken ct = default) =>
        api.PutAsync($"{Shops}/{shopId}/verified", new VerifiedRequest(verified), ct);

    /// <summary>Block (hide) or unblock a shop. Only the shop: its owner is unaffected.</summary>
    public Task<ApiResult> SetBlockedAsync(Guid shopId, bool blocked, CancellationToken ct = default) =>
        api.PutAsync($"{Shops}/{shopId}/blocked", new BlockedRequest(blocked), ct);

    // ───────────────────────────── 2. Dish names ─────────────────────────────

    /// <summary>Names sellers suggested, oldest first.</summary>
    public Task<ApiResult<List<AdminDishSuggestionDto>>> GetDishSuggestionsAsync(CancellationToken ct = default) =>
        api.GetAsync<List<AdminDishSuggestionDto>>($"{DishNames}/suggestions", ct);

    /// <summary>Every approved dish name with its price range in each country.</summary>
    public Task<ApiResult<List<AdminDishCatalogDto>>> GetDishNamesAsync(CancellationToken ct = default) =>
        api.GetAsync<List<AdminDishCatalogDto>>(DishNames, ct);

    /// <summary>Sets a name's price range in one country; returns how many dishes now need a new price (their
    /// sellers are emailed).</summary>
    public Task<ApiResult<int>> SetPriceRangeAsync(Guid id, Country country, decimal min, decimal max,
                                                  CancellationToken ct = default) =>
        api.PutAsync<int>($"{DishNames}/{id}/price-ranges/{country}", new PriceRangeRequest(min, max), ct);

    public Task<ApiResult> ApproveDishNameAsync(Guid id, CancellationToken ct = default) =>
        api.PostAsync($"{DishNames}/{id}/approve", null, ct);

    public Task<ApiResult> RejectDishNameAsync(Guid id, CancellationToken ct = default) =>
        api.PostAsync($"{DishNames}/{id}/reject", null, ct);

    // ───────────────────────────── 3. Users ─────────────────────────────

    /// <summary>Everyone who has signed up, newest first; optionally a name search, or only suspended people; one page.</summary>
    public Task<ApiResult<PagedResult<AdminUserDto>>> GetUsersAsync(string? search, bool suspendedOnly, int page,
                                                                   int pageSize, CancellationToken ct = default)
    {
        var path = $"{Users}?page={page}&pageSize={pageSize}&suspendedOnly={suspendedOnly.ToString().ToLowerInvariant()}";
        if (!string.IsNullOrWhiteSpace(search)) path += $"&search={Uri.EscapeDataString(search.Trim())}";
        return api.GetAsync<PagedResult<AdminUserDto>>(path, ct);
    }

    /// <summary>Suspends someone until a date (UTC) or until further notice (null); their shop goes with them.</summary>
    public Task<ApiResult<UserSuspendedDto>> SuspendAsync(Guid userId, string reason, DateTime? untilUtc,
                                                          CancellationToken ct = default) =>
        api.PostAsync<UserSuspendedDto>($"{Users}/{userId}/suspend", new SuspendRequest(reason, untilUtc), ct);

    /// <summary>Lifts a suspension.</summary>
    public Task<ApiResult> ReinstateAsync(Guid userId, CancellationToken ct = default) =>
        api.PostAsync($"{Users}/{userId}/reinstate", null, ct);

    /// <summary>Gives someone a role (one of <see cref="Roles.Assignable"/>).</summary>
    public Task<ApiResult> GrantRoleAsync(Guid userId, string role, CancellationToken ct = default) =>
        api.PutAsync($"{Users}/{userId}/roles/{Uri.EscapeDataString(role)}", null, ct);

    /// <summary>Takes a role away (they're signed out everywhere). The API refuses your own Admin role and the last
    /// admin's.</summary>
    public Task<ApiResult> RemoveRoleAsync(Guid userId, string role, CancellationToken ct = default) =>
        api.DeleteAsync($"{Users}/{userId}/roles/{Uri.EscapeDataString(role)}", ct);
}
