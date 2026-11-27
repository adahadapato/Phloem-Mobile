using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>One dish card (Home, Browse &amp; Order). Same shape as the API's <c>DishCardDto</c>.</summary>
/// <remarks>ImageUrl is the 400×300 thumbnail; null when the dish has no photo yet.</remarks>
public sealed record DishCardDto(
    Guid Id,
    string Name,
    DishCategory Category,
    string? Description,
    decimal Price,
    Currency Currency,
    Country Country,
    decimal Rating,
    int RatingCount,
    string? ImageUrl,
    Guid VendorId,
    string VendorName,
    bool IsVerifiedShop,
    string? PronunciationGuide = null,
    string? PronunciationAudioUrl = null);
