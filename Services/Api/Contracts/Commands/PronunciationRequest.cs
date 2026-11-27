namespace Phloem.Services.Api.Contracts.Commands;

/// <summary>Body of "set pronunciation" (admin): how a dish name sounds and its other spellings. Same shape as the
/// API's <c>DishCatalogEndpoints.PronunciationRequest</c>.</summary>
/// <param name="PronunciationGuide">"WAH-chay"; blank = none.</param>
/// <param name="Aliases">Other spellings / mishearings (max 10); empty = none.</param>
public sealed record PronunciationRequest(string? PronunciationGuide, IReadOnlyList<string>? Aliases);
