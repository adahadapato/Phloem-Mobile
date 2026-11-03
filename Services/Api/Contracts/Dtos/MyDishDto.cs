using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>One of my dishes, for the dashboard and the dish editor. Same shape as the API's <c>MyDishDto</c>.
/// <see cref="NeedsPriceUpdate"/>: the admin moved the allowed range and the price is now outside it.</summary>
public sealed record MyDishDto(
    Guid Id,
    Guid DishCatalogId,
    string CatalogName,
    string? Detail,
    string Name,
    DishCategory Category,
    decimal Price,
    Currency Currency,
    decimal? AllowedMinPrice,
    decimal? AllowedMaxPrice,
    bool NeedsPriceUpdate,
    string? Description,
    bool IsAvailable,
    decimal Rating,
    int RatingCount,
    IReadOnlyList<DishPhotoDto> Photos,
    ContentReviewStatus ReviewStatus = ContentReviewStatus.Approved,
    string? ReviewNote = null);
