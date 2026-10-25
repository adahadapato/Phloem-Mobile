using System.Text.Json;
using Microsoft.Extensions.Logging;
using Phloem.Services.Api;
using Phloem.Services.Api.Contracts.Commands;
using Phloem.Services.Api.Contracts.Dtos;

namespace Phloem.Services;

/// <summary>
/// The "what can be sent" list, from the API's database, so an admin can change it without an app update.
/// <list type="bullet">
///   <item><description>Everyone: <c>GET /api/parcel-rules</c> (Send food to the UK shows it). The last list is kept
///   on the phone, so it still shows without internet.</description></item>
///   <item><description>Admin: add, change, delete and reorder lines (<c>/api/admin/parcel-rules</c>).</description></item>
/// </list>
/// </summary>
public sealed class ParcelRulesService(ApiClient api, ILogger<ParcelRulesService> logger)
{
    private const string SavedListKey = "phloem.parcelrules";
    private const string AdminBase = "api/admin/parcel-rules";

    /// <summary>The list (and keeps it on the phone); without internet, the list kept last time.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The list and null; or null and why it couldn't be loaded (nothing kept either).</returns>
    public async Task<(IReadOnlyList<ParcelItemRuleDto>? Rules, ApiError? Error)> GetAsync(CancellationToken ct = default)
    {
        var result = await api.GetAsync<List<ParcelItemRuleDto>>("api/parcel-rules", ct);
        if (result.IsSuccess && result.Value is not null)
        {
            Save(result.Value);
            return (result.Value, null);
        }

        logger.LogInformation("Parcel rules not loaded from the API ({Code}); using the saved copy.", result.Error?.Code);
        var saved = ReadSaved();
        return saved is not null ? (saved, null) : (null, result.Error);
    }

    /// <summary>The list straight from the API, never the saved copy (Admin → What can be sent).</summary>
    public Task<ApiResult<List<ParcelItemRuleDto>>> GetLiveAsync(CancellationToken ct = default) =>
        api.GetAsync<List<ParcelItemRuleDto>>("api/parcel-rules", ct);

    /// <summary>Adds a line at the end of the list.</summary>
    public Task<ApiResult<Guid>> AddAsync(ParcelItemRuleRequest body, CancellationToken ct = default) =>
        api.PostAsync<Guid>(AdminBase, body, ct);

    /// <summary>Changes a line's kind, name or detail.</summary>
    public Task<ApiResult> UpdateAsync(Guid id, ParcelItemRuleRequest body, CancellationToken ct = default) =>
        api.PutAsync($"{AdminBase}/{id}", body, ct);

    /// <summary>Removes a line.</summary>
    public Task<ApiResult> DeleteAsync(Guid id, CancellationToken ct = default) =>
        api.DeleteAsync($"{AdminBase}/{id}", ct);

    /// <summary>Puts the list in a new order (every line's id, first to last).</summary>
    public Task<ApiResult> ReorderAsync(IReadOnlyList<Guid> ids, CancellationToken ct = default) =>
        api.PutAsync($"{AdminBase}/order", new ReorderRequest(ids), ct);

    private static void Save(List<ParcelItemRuleDto> rules)
    {
        try { Preferences.Default.Set(SavedListKey, JsonSerializer.Serialize(rules, ApiJson.Options)); }
        catch (Exception) { /* storage unavailable: the page still shows what it loaded */ }
    }

    private static List<ParcelItemRuleDto>? ReadSaved()
    {
        try
        {
            var json = Preferences.Default.Get(SavedListKey, string.Empty);
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<List<ParcelItemRuleDto>>(json, ApiJson.Options);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
