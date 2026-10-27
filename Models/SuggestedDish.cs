namespace Phloem.Models;

/// <summary>
/// A dish card in Home's suggestion rows ("Order again", "You might like", "Popular in Ghana"), with the reason it
/// was picked shown under the shop's name.
/// </summary>
public sealed class SuggestedDish
{
    public required Dish Dish { get; init; }

    /// <summary>e.g. "Often ordered with Jollof Rice"; empty in "Order again".</summary>
    public string Reason { get; init; } = string.Empty;

    public bool HasReason => !string.IsNullOrWhiteSpace(Reason);
}

/// <summary>Home's suggestion rows, ready to show.</summary>
/// <param name="OrderAgain">Dishes bought before, on sale now (signed in only).</param>
/// <param name="ForYou">New dishes with reasons.</param>
/// <param name="IsPersonal">False = no order history (or signed out): the row is "Popular in …".</param>
public sealed record HomeSuggestions(
    IReadOnlyList<SuggestedDish> OrderAgain,
    IReadOnlyList<SuggestedDish> ForYou,
    bool IsPersonal);
