namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>
/// "Which LGA is this town in?" Confidence is "known", "high", "low" or "none".
/// Same shape as the API's <c>TownSuggestionDto</c>.
/// </summary>
public sealed record TownSuggestionDto(string TownName, Guid? TownId, Guid? DistrictId, string? DistrictName,
                                       string Confidence, bool FromAi);
