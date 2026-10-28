using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Models;

/// <summary>
/// A line in the list under Home's search bar: "🍲 Jollof Rice · 7 dishes" or "🏪 Auntie Ama's Kitchen · Shop",
/// with the typed part shown in bold (<see cref="Before"/> + <see cref="Match"/> + <see cref="After"/>).
/// </summary>
public sealed class SearchPrediction
{
    public required string Text { get; init; }
    public required SearchSuggestionKind Kind { get; init; }
    public int DishCount { get; init; }
    public Guid? ShopId { get; init; }

    /// <summary>What was typed when this list was made.</summary>
    public string Typed { get; init; } = string.Empty;

    public bool IsShop => Kind == SearchSuggestionKind.Shop;
    public string Icon => IsShop ? "🏪" : "🍲";
    public string Meta => IsShop ? "Shop" : DishCount > 1 ? $"{DishCount} dishes" : string.Empty;

    private int At => string.IsNullOrEmpty(Typed) ? -1 : Text.IndexOf(Typed, StringComparison.OrdinalIgnoreCase);
    public string Before => At < 0 ? Text : Text[..At];
    public string Match => At < 0 ? string.Empty : Text.Substring(At, Typed.Length);
    public string After => At < 0 ? string.Empty : Text[(At + Typed.Length)..];
}
