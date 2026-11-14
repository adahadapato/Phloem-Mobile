namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>A state / region. Same shape as the API's <c>RegionDto</c>.</summary>
public sealed record RegionDto(Guid Id, string Name, string Code);
