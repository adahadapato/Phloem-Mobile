using Phloem.Services.Api;
using Phloem.Services.Api.Contracts.Commands;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services;

/// <summary>A dish name sellers pick from, with its allowed price range in the seller's country.</summary>
public record CatalogDish(string Name, string Category, string Group)
{
    /// <summary>The name's Id on the API.</summary>
    public Guid Id { get; init; }

    public DishCategory ApiCategory { get; init; }
    public CatalogGroup ApiGroup { get; init; }

    /// <summary>False = an admin hasn't set its price range yet, so it can't be sold (shown greyed out).</summary>
    public bool HasPriceRange { get; init; }

    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public string CurrencySymbol { get; init; } = string.Empty;

    /// <summary>"GH₵40 – GH₵90", or "Price not set yet".</summary>
    public string RangeText => HasPriceRange
        ? $"{CurrencySymbol}{MinPrice:0.##} – {CurrencySymbol}{MaxPrice:0.##}"
        : "Price not set yet";

    /// <summary>Flag / icon shown next to the group in the picker.</summary>
    /// <summary>How the name sounds ("WAH-chay"); null when staff haven't added it.</summary>
    public string? PronunciationGuide { get; init; }

    /// <summary>The recording of the name (the 🔊); null when there isn't one.</summary>
    public string? PronunciationAudioUrl { get; init; }

    /// <summary>True when there's a recording to play.</summary>
    public bool HasSound => !string.IsNullOrEmpty(PronunciationAudioUrl);

    /// <summary>True when the written guide shows.</summary>
    public bool HasGuide => !string.IsNullOrEmpty(PronunciationGuide);

    public string GroupIcon => Group switch
    {
        "Ghana" => "🇬🇭",
        "Nigeria" => "🇳🇬",
        "Drinks" => "🥤",
        "Snacks" => "🍪",
        _ => "🌍"
    };
}

/// <summary>
/// The dish names sellers choose from (so there are no typos), from the API (<c>GET /api/dish-catalog</c>) with the
/// price range in the seller's country; plus "Can't find your dish?" suggestions for an admin to approve.
/// </summary>
public sealed class DishCatalogService(ApiClient api)
{
    /// <summary>The names last loaded (empty until <see cref="LoadAsync"/>).</summary>
    public IReadOnlyList<CatalogDish> Dishes { get; private set; } = [];

    /// <summary>The names and their ranges in a country (the seller's shop country).</summary>
    /// <returns>Null when loaded; otherwise why not.</returns>
    public async Task<string?> LoadAsync(Country country, CancellationToken ct = default)
    {
        var result = await api.GetAsync<List<DishCatalogItemDto>>($"api/dish-catalog?country={country}", ct);
        if (result.IsFailure) return result.Error!.DisplayMessage;

        Dishes = result.Value!.Select(ToCatalogDish).ToList();
        return null;
    }

    /// <summary>"Can't find your dish?": a new name for an admin to approve.</summary>
    public Task<ApiResult<Guid>> SuggestAsync(string name, DishCategory category, CatalogGroup group,
                                              CancellationToken ct = default) =>
        api.PostAsync<Guid>("api/dish-catalog/suggestions", new SuggestDishNameCommand(name.Trim(), category, group), ct);

    /// <summary>A category as the screens show it.</summary>
    public static string CategoryText(DishCategory category) => category switch
    {
        DishCategory.LocalMeals => "Local Meals",
        DishCategory.Snacks => "Snacks",
        DishCategory.Drinks => "Drinks",
        _ => category.ToString()
    };

    /// <summary>A group as the screens show it.</summary>
    public static string GroupText(CatalogGroup group) => group switch
    {
        CatalogGroup.WestAfrican => "West African",
        _ => group.ToString()
    };

    /// <summary>The group a seller's suggestion goes in: their own country's, else West African.</summary>
    public static CatalogGroup GroupFor(Country country) => country switch
    {
        Country.Ghana => CatalogGroup.Ghana,
        Country.Nigeria => CatalogGroup.Nigeria,
        _ => CatalogGroup.WestAfrican
    };

    private static CatalogDish ToCatalogDish(DishCatalogItemDto d) =>
        new(d.Name, CategoryText(d.Category), GroupText(d.Group))
        {
            Id = d.Id,
            ApiCategory = d.Category,
            ApiGroup = d.Group,
            HasPriceRange = d.HasPriceRange,
            MinPrice = d.MinPrice,
            MaxPrice = d.MaxPrice,
            CurrencySymbol = d.Currency is { } c ? OrderService.SymbolOf(c) : string.Empty,
            PronunciationGuide = d.PronunciationGuide,
            PronunciationAudioUrl = d.PronunciationAudioUrl
        };
}
