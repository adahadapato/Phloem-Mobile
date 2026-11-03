using CommunityToolkit.Mvvm.ComponentModel;

namespace Phloem.Models;

public partial class Dish : ObservableObject
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;
    public string ImageUrl { get; set; } = string.Empty;

    [ObservableProperty]
    public partial decimal Price { get; set; }
    public double Rating { get; set; }
    public string Category { get; set; } = "Popular";
    public string Country { get; set; } = "Ghana";
    public string VendorName { get; set; } = string.Empty;
    public string VendorId { get; set; } = string.Empty;
    public string CatalogName { get; set; } = string.Empty;  // the list name, e.g. "Jollof Rice"
    public string Description { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;

    /// <summary>Seller's dishes: why buyers can't see it yet ("Being checked", "Not allowed: …"); null when fine.</summary>
    public string? ReviewText { get; set; }

    /// <summary>True when <see cref="ReviewText"/> should show.</summary>
    public bool HasReviewText => !string.IsNullOrEmpty(ReviewText);

    private string? _currencySymbol;

    /// <summary>
    /// The price's currency symbol, e.g. "GH₵", "₦", "£" (from the API). When not set, it's taken from the dish's
    /// country.
    /// </summary>
    public string CurrencySymbol
    {
        get => _currencySymbol
               ?? CountryOption.All.FirstOrDefault(c => c.Name == Country)?.CurrencySymbol
               ?? string.Empty;
        set => _currencySymbol = value;
    }

    /// <summary>The price as shown on cards, e.g. "₦4,500.00".</summary>
    public string PriceText => $"{CurrencySymbol}{Price:N2}";

    partial void OnPriceChanged(decimal value) => OnPropertyChanged(nameof(PriceText));
}
