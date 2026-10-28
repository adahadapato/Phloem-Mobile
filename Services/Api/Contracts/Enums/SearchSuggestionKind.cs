namespace Phloem.Services.Api.Contracts.Enums;

/// <summary>What a search prediction points at. Same names as the API's <c>SearchSuggestionKind</c>.</summary>
public enum SearchSuggestionKind
{
    /// <summary>A dish name: picking it searches for it.</summary>
    Dish = 1,

    /// <summary>A shop: picking it opens the shop's menu.</summary>
    Shop = 2
}
