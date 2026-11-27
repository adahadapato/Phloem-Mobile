using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>
/// One name in the dish picker with its allowed price range in my country (<c>GET /api/dish-catalog</c>). Same shape as
/// the API's <c>DishCatalogItemDto</c>. <see cref="HasPriceRange"/> false = can't be sold yet (no range set).
/// </summary>
public sealed record DishCatalogItemDto(
    Guid Id,
    string Name,
    DishCategory Category,
    CatalogGroup Group,
    bool HasPriceRange,
    decimal? MinPrice,
    decimal? MaxPrice,
    Currency? Currency,
    string? PronunciationGuide = null,
    string? PronunciationAudioUrl = null);
