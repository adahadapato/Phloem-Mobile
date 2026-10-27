namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>
/// <c>GET /api/me/suggestions</c>: Home's "Order again" and "For you" rows. Same shape as the API's
/// <c>SuggestionsDto</c>.
/// </summary>
/// <param name="OrderAgain">Dishes I've bought that can be ordered now in this country, most recent first.</param>
/// <param name="ForYou">New dishes picked from what I buy, each with its reason.</param>
/// <param name="IsPersonal">False when I have no orders yet: <paramref name="ForYou"/> is then "Popular in …".</param>
public sealed record SuggestionsDto(
    IReadOnlyList<DishCardDto> OrderAgain,
    IReadOnlyList<SuggestedDishDto> ForYou,
    bool IsPersonal);

/// <summary>A suggested dish and the reason shown on it ("Often ordered with Jollof Rice").</summary>
public sealed record SuggestedDishDto(DishCardDto Dish, string Reason);
