using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>
/// A country's states / regions and what the levels are called ("State" / "LGA"). Empty list = no list for that
/// country. Same shape as the API's <c>CountryLocationsDto</c>.
/// </summary>
public sealed record CountryLocationsDto(Country Country, string RegionLabel, string DistrictLabel,
                                         IReadOnlyList<RegionDto> Regions);
