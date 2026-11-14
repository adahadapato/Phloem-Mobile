namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>
/// A suggestion under the Town box: a known town, or an LGA's name (then <see cref="TownId"/> is null).
/// Same shape as the API's <c>TownDto</c>.
/// </summary>
public sealed record TownDto(Guid? TownId, string Name, Guid RegionId, string RegionName, Guid DistrictId,
                             string DistrictName)
{
    /// <summary>"Eti-Osa, Lagos" or "LGA in Lagos", under the name.</summary>
    public string Where => TownId is null ? $"LGA in {RegionName}" : $"{DistrictName}, {RegionName}";
}
