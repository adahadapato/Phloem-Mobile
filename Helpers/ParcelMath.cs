using System.Globalization;

namespace Phloem.Helpers;

/// <summary>
/// The same box maths as the API (Domain ParcelBox), so the form can say "This box counts as 12 kg" at once while the
/// server's quote is on its way. The server's quote is always the price shown.
/// </summary>
public static class ParcelMath
{
    /// <summary>Same limits as the API.</summary>
    public const decimal MinBoxKg = 0.1m, MaxBoxKg = 500m, MinSideCm = 1m, MaxSideCm = 300m, MaxParcelKg = 1000m;

    /// <summary>Same limits as the API.</summary>
    public const int MaxQuantity = 50, MaxLines = 20;

    /// <summary>"4.5" or "4,5" → 4.5 (whatever the phone's language); null when it isn't a number.</summary>
    public static decimal? Parse(string? text) =>
        decimal.TryParse((text ?? string.Empty).Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint,
                         CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    /// <summary>Rounds UP to the next 0.5 kg.</summary>
    public static decimal RoundUpHalf(decimal kg) => Math.Ceiling(kg / 0.5m) * 0.5m;

    /// <summary>L × W × H ÷ divisor, to 2 decimals.</summary>
    public static decimal VolumeKg(decimal length, decimal width, decimal height, int divisor) =>
        decimal.Round(length * width * height / divisor, 2, MidpointRounding.AwayFromZero);

    /// <summary>"12 kg", "7.5 kg".</summary>
    public static string Kg(decimal kg) => $"{kg.ToString("0.##", CultureInfo.InvariantCulture)} kg";
}
