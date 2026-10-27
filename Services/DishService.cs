using Microsoft.Extensions.Options;
using Phloem.Models;
using Phloem.Services.Api;
using Phloem.Services.Api.Contracts;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;
using Phloem.Settings;

namespace Phloem.Services;

/// <summary>
/// Dishes for buyers: <c>/api/dishes</c>. Returns the app's own <see cref="Dish"/> model, so the existing screens
/// bind to it unchanged (name, photo, price, rating, shop).
/// </summary>
public sealed class DishService(ApiClient api, IOptions<ApiSettings> settings)
{
    /// <summary>The category chips' names, as the screens show them.</summary>
    private static readonly IReadOnlyDictionary<DishCategory, string> CategoryNames = new Dictionary<DishCategory, string>
    {
        [DishCategory.LocalMeals] = "Local Meals",
        [DishCategory.Snacks] = "Snacks",
        [DishCategory.Drinks] = "Drinks"
    };

    /// <summary>Most dishes one search returns when no page size is given (e.g. Home's popular dishes).</summary>
    public const int PageSize = 50;

    /// <summary>Searches dishes that can be ordered now (approved shops, available, priced).</summary>
    /// <param name="country">Only this country; null = every country.</param>
    /// <param name="category">A chip's name ("Local Meals", "Snacks", "Drinks"); anything else = every category.</param>
    /// <param name="search">Text typed in the search bar; empty = no text filter.</param>
    /// <param name="shopId">Only this shop's menu; null = every shop.</param>
    /// <param name="sort">Best rated first, or by price.</param>
    /// <param name="page">1-based page.</param>
    /// <param name="pageSize">Dishes per page.</param>
    /// <param name="ct">Cancels the search (e.g. the user typed another letter).</param>
    /// <returns>The dishes, or why they couldn't be loaded.</returns>
    public async Task<ApiResult<PagedResult<Dish>>> SearchAsync(Country? country, string? category, string? search,
                                                               string? shopId, DishSort sort = DishSort.Rating,
                                                               int page = 1, int pageSize = PageSize,
                                                               CancellationToken ct = default)
    {
        var query = new Dictionary<string, string?>
        {
            ["country"] = country?.ToString(),
            ["category"] = CategoryFromName(category)?.ToString(),
            ["search"] = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
            ["shopId"] = Guid.TryParse(shopId, out var id) ? id.ToString() : null,
            ["sort"] = sort.ToString(),
            ["page"] = page.ToString(),
            ["pageSize"] = pageSize.ToString()
        };

        var result = await api.GetAsync<PagedResult<DishCardDto>>(ApiQuery.Build("api/dishes", query), ct);
        if (result.IsFailure) return result.Error!;

        var paged = result.Value!;
        return ApiResult<PagedResult<Dish>>.Success(
            new PagedResult<Dish>(paged.Items.Select(ToDish).ToList(), paged.TotalCount, paged.Page, paged.PageSize));
    }

    /// <summary>A chip's name as the API's category; null for "All" / "Popular" (no filter).</summary>
    public static DishCategory? CategoryFromName(string? name) =>
        CategoryNames.FirstOrDefault(c => string.Equals(c.Value, name, StringComparison.OrdinalIgnoreCase)) is
            { Value: not null } match ? match.Key : null;

    /// <summary>The API's dish card as the app's <see cref="Dish"/> (also used by <see cref="SuggestionService"/>).</summary>
    internal Dish ToDish(DishCardDto d) => new()
    {
        Id = d.Id.ToString(),
        Name = d.Name,
        ImageUrl = settings.Value.ResolveFileUrl(d.ImageUrl) ?? string.Empty,
        Price = d.Price,
        Rating = (double)d.Rating,
        Category = CategoryNames.GetValueOrDefault(d.Category, d.Category.ToString()),
        Country = CountryOption.NameOf(d.Country),
        CurrencySymbol = CountryOption.FromApi(d.Country).CurrencySymbol,
        VendorName = d.VendorName,
        VendorId = d.VendorId.ToString(),
        Description = d.Description ?? string.Empty,
        IsAvailable = true
    };
}
