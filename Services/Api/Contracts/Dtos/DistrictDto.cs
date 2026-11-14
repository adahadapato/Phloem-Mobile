namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>An LGA / district. Same shape as the API's <c>DistrictDto</c>.</summary>
public sealed record DistrictDto(Guid Id, string Name);
