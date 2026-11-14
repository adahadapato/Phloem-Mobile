using CommunityToolkit.Mvvm.ComponentModel;
using Phloem.Helpers;
using Phloem.Services.Api.Contracts.Dtos;

namespace Phloem.Models;

/// <summary>
/// One box line as typed (Send a parcel, and staff weighing at the hub): weight of ONE box, its outside size and how
/// many identical boxes. Works out at once what it counts as ("This box counts as 19.5 kg").
/// </summary>
public partial class ParcelBoxDraft : ObservableObject
{
    /// <summary>The route's divisor (usually 5000); set by the page.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountsText), nameof(ChargedBySize))]
    public partial int Divisor { get; set; } = 5000;

    /// <summary>"Box 1", "Box 2"… (set by the page when boxes are added or removed).</summary>
    [ObservableProperty]
    public partial string Title { get; set; } = "Your box";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountsText), nameof(ChargedBySize))]
    public partial string Weight { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountsText), nameof(ChargedBySize))]
    public partial string Length { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountsText), nameof(ChargedBySize))]
    public partial string Width { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountsText), nameof(ChargedBySize))]
    public partial string Height { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CountsText), nameof(ChargedBySize))]
    public partial string Quantity { get; set; } = "1";

    /// <summary>Show "Remove" (more than one box).</summary>
    [ObservableProperty]
    public partial bool CanRemove { get; set; }

    // ───────────────────────────── Figures ─────────────────────────────

    private decimal? WeightKg => ParcelMath.Parse(Weight) is { } w && w >= ParcelMath.MinBoxKg && w <= ParcelMath.MaxBoxKg ? w : null;

    private decimal? Side(string text) =>
        ParcelMath.Parse(text) is { } s && s >= ParcelMath.MinSideCm && s <= ParcelMath.MaxSideCm ? s : null;

    private int SidesTyped => new[] { Length, Width, Height }.Count(t => !string.IsNullOrWhiteSpace(t));

    /// <summary>All three sides typed and within the limits.</summary>
    public bool HasSize => Side(Length) is not null && Side(Width) is not null && Side(Height) is not null;

    /// <summary>How many boxes (1 while not a whole number in range).</summary>
    public int Count => int.TryParse(Quantity, out var q) && q is >= 1 and <= ParcelMath.MaxQuantity ? q : 1;

    private bool QuantityOk => int.TryParse(Quantity, out var q) && q is >= 1 and <= ParcelMath.MaxQuantity;

    /// <summary>The size weight of ONE box, or null without a size.</summary>
    public decimal? VolumeKg => HasSize ? ParcelMath.VolumeKg(Side(Length)!.Value, Side(Width)!.Value, Side(Height)!.Value, Divisor) : null;

    /// <summary>What ONE box is charged as, or null while the weight is missing.</summary>
    public decimal? ChargeableEach => WeightKg is { } w ? ParcelMath.RoundUpHalf(Math.Max(w, VolumeKg ?? 0)) : null;

    /// <summary>Its size, not its weight, sets the price.</summary>
    public bool ChargedBySize => WeightKg is { } w && VolumeKg is { } v && v > w;

    /// <summary>Weight and quantity in range, and all three sides or none.</summary>
    public bool IsValid => WeightKg is not null && QuantityOk && (SidesTyped == 0 || HasSize);

    /// <summary>Real weight of the line (0 while not typed).</summary>
    public decimal TotalWeightKg => (WeightKg ?? 0) * Count;

    /// <summary>"This box counts as 19.5 kg: it weighs 8 kg, but its size counts as 19.2 kg."</summary>
    public string CountsText
    {
        get
        {
            if (SidesTyped is > 0 and < 3) return "Please give all three sides (length, width and height), or leave them all empty.";
            if (SidesTyped == 3 && !HasSize) return $"Each side must be {ParcelMath.MinSideCm:0}–{ParcelMath.MaxSideCm:0} cm.";
            if (ChargeableEach is not { } each) return "Enter the weight of one box.";

            var text = ChargedBySize
                ? $"This box counts as {ParcelMath.Kg(each)}: it weighs {ParcelMath.Kg(WeightKg!.Value)}, but its size counts as {ParcelMath.Kg(VolumeKg!.Value)}. A smaller box would cost less."
                : HasSize
                    ? $"This box counts as {ParcelMath.Kg(each)} (its size counts as {ParcelMath.Kg(VolumeKg!.Value)}, less than its weight)."
                    : $"This box counts as {ParcelMath.Kg(each)} (rounded up to the next 0.5 kg).";
            return Count > 1 ? $"{text} × {Count} boxes = {ParcelMath.Kg(each * Count)}." : text;
        }
    }

    /// <summary>The line for the API (call only when <see cref="IsValid"/>).</summary>
    public ParcelBoxInputDto ToInput() => new(
        WeightKg!.Value,
        HasSize ? Side(Length) : null,
        HasSize ? Side(Width) : null,
        HasSize ? Side(Height) : null,
        Count);

    /// <summary>Starts a hub line from what the sender declared (staff overwrite the weight and size).</summary>
    public static ParcelBoxDraft FromDeclared(ShipmentBoxDto box, int divisor) => new()
    {
        Divisor = divisor,
        Quantity = box.Quantity.ToString(),
        Length = box.LengthCm?.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
        Width = box.WidthCm?.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
        Height = box.HeightCm?.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
    };
}
