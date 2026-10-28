using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>
/// One search prediction from <c>GET /api/dishes/suggest</c>: a dish name or a shop. Same shape as the API's
/// <c>SearchSuggestionDto</c>.
/// </summary>
/// <param name="Text">The full name to show (and to search for, for a dish).</param>
/// <param name="Kind">Dish or Shop.</param>
/// <param name="DishCount">Dish: how many dishes have this name.</param>
/// <param name="ShopId">Shop only.</param>
public sealed record SearchSuggestionDto(string Text, SearchSuggestionKind Kind, int DishCount, Guid? ShopId);
