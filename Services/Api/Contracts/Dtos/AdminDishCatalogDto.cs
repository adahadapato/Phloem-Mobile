using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>Admin → one approved dish name with its price range in every country (<c>GET /api/admin/dish-catalog</c>).
/// Same shape as the API's <c>AdminDishCatalogDto</c>.</summary>
public sealed record AdminDishCatalogDto(
    Guid Id,
    string Name,
    DishCategory Category,
    CatalogGroup Group,
    IReadOnlyList<DishPriceRangeDto> PriceRanges,
    string? PronunciationGuide = null,
    IReadOnlyList<string>? Aliases = null,
    string? PronunciationAudioUrl = null);
