using Phloem.Models;
using Phloem.Services.Api;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services;

/// <summary>
/// Home's suggestions, the same as the website's:
/// <list type="bullet">
///   <item><description>Signed in: <c>GET /api/me/suggestions</c> ("Order again" + "For you", personal).</description></item>
///   <item><description>Signed out: <c>GET /api/dishes/popular</c> ("Popular in Ghana").</description></item>
/// </list>
/// Only dishes that can be ordered now in the country; returned as the app's <see cref="Dish"/> model.
/// </summary>
public sealed class SuggestionService(ApiClient api, DishService dishes, SessionService session)
{
    /// <summary>How many dishes in each row.</summary>
    public const int Take = 10;

    /// <summary>The rows for whoever is using the app (signed in or not).</summary>
    /// <param name="country">The country Home shows.</param>
    /// <param name="ct">Cancels the call (e.g. the country changed again).</param>
    public async Task<ApiResult<HomeSuggestions>> GetForHomeAsync(Country country, CancellationToken ct = default)
    {
        var query = new Dictionary<string, string?> { ["country"] = country.ToString(), ["take"] = Take.ToString() };

        if (!session.IsLoggedIn)
        {
            var popular = await api.GetAsync<List<SuggestedDishDto>>(ApiQuery.Build("api/dishes/popular", query), ct);
            if (popular.IsFailure) return popular.Error!;
            return ApiResult<HomeSuggestions>.Success(new HomeSuggestions([], popular.Value!.Select(ToSuggested).ToList(), false));
        }

        var mine = await api.GetAsync<SuggestionsDto>(ApiQuery.Build("api/me/suggestions", query), ct);
        if (mine.IsFailure) return mine.Error!;

        var value = mine.Value!;
        return ApiResult<HomeSuggestions>.Success(new HomeSuggestions(
            value.OrderAgain.Select(d => new SuggestedDish { Dish = dishes.ToDish(d) }).ToList(),
            value.ForYou.Select(ToSuggested).ToList(),
            value.IsPersonal));
    }

    private SuggestedDish ToSuggested(SuggestedDishDto s) => new() { Dish = dishes.ToDish(s.Dish), Reason = s.Reason };
}
