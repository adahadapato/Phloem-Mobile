using Phloem.Services.Api;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services;

/// <summary>
/// States / regions, LGAs / districts and towns for the recipient's address on Send a parcel (anyone may call these).
/// A country's states and a state's LGAs hardly ever change, so they're kept in memory while the app runs.
/// </summary>
public sealed class LocationService(ApiClient api)
{
    private readonly Dictionary<Country, CountryLocationsDto> _regions = new();
    private readonly Dictionary<Guid, List<DistrictDto>> _districts = new();

    /// <summary>A country's states / regions (empty list = no list for that country) and their labels.</summary>
    public async Task<ApiResult<CountryLocationsDto>> GetRegionsAsync(Country country, CancellationToken ct = default)
    {
        if (_regions.TryGetValue(country, out var kept)) return ApiResult<CountryLocationsDto>.Success(kept);

        var result = await api.GetAsync<CountryLocationsDto>($"api/locations/{country}/regions", ct);
        if (result.IsSuccess) _regions[country] = result.Value!;
        return result;
    }

    /// <summary>A state's LGAs, A–Z.</summary>
    public async Task<ApiResult<List<DistrictDto>>> GetDistrictsAsync(Guid regionId, CancellationToken ct = default)
    {
        if (_districts.TryGetValue(regionId, out var kept)) return ApiResult<List<DistrictDto>>.Success(kept);

        var result = await api.GetAsync<List<DistrictDto>>($"api/locations/regions/{regionId}/districts", ct);
        if (result.IsSuccess) _districts[regionId] = result.Value!;
        return result;
    }

    /// <summary>Town suggestions as the customer types (known towns and LGA names; small typos forgiven).</summary>
    public Task<ApiResult<List<TownDto>>> SearchTownsAsync(Country country, string text, Guid? regionId,
                                                           CancellationToken ct = default)
    {
        var path = $"api/locations/{country}/towns?text={Uri.EscapeDataString(text)}&take=8";
        if (regionId is { } id) path += $"&regionId={id}";
        return api.GetAsync<List<TownDto>>(path, ct);
    }

    /// <summary>Which LGA of the state a typed town is in (our list first, then the AI). The customer confirms it.</summary>
    public Task<ApiResult<TownSuggestionDto>> SuggestDistrictAsync(Guid regionId, string townName,
                                                                   CancellationToken ct = default) =>
        api.PostAsync<TownSuggestionDto>("api/locations/towns/suggest", new { regionId, townName }, ct);
}
