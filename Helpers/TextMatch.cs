using System.Globalization;
using System.Text;

namespace Phloem.Helpers;

/// <summary>
/// Typo-tolerant matching for place names, the same rules as the API's TownMatching and the website:
/// "port harcort" finds "Port Harcourt", "lagso" finds "Lagos".
/// </summary>
public static class TextMatch
{
    /// <summary>"  Lekki-Phase 1. " → "lekki phase 1": lower case, letters and digits, single spaces, no accents.</summary>
    public static string Normalise(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var sb = new StringBuilder(text.Length);
        var lastWasSpace = true;
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c)) { sb.Append(char.ToLowerInvariant(c)); lastWasSpace = false; }
            else if (!lastWasSpace) { sb.Append(' '); lastWasSpace = true; }
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>0 exact, 1 starts with, 2 a word starts with, 3 contains, 10 + n typing mistakes away; null = no match.</summary>
    public static int? Score(string candidate, string typed)
    {
        var c = Normalise(candidate);
        var t = Normalise(typed);
        if (t.Length == 0 || c == t) return 0;
        if (c.StartsWith(t, StringComparison.Ordinal)) return 1;
        if (c.Contains(' ' + t, StringComparison.Ordinal)) return 2;
        if (c.Contains(t, StringComparison.Ordinal)) return 3;

        var allowed = t.Length switch { < 4 => 0, < 7 => 1, _ => 2 };
        if (allowed == 0) return null;
        var best = Math.Min(Distance(c, t, allowed),
                            c.Length > t.Length ? Distance(c[..t.Length], t, allowed) : int.MaxValue);
        return best <= allowed ? 10 + best : null;
    }

    /// <summary>Edit distance where swapping two neighbouring letters counts as one mistake.</summary>
    private static int Distance(string a, string b, int max)
    {
        if (Math.Abs(a.Length - b.Length) > max) return int.MaxValue;
        var d = new int[a.Length + 1, b.Length + 1];
        for (var j = 0; j <= b.Length; j++) d[0, j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            d[i, 0] = i;
            var rowBest = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var value = Math.Min(Math.Min(d[i, j - 1] + 1, d[i - 1, j] + 1), d[i - 1, j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1]) value = Math.Min(value, d[i - 2, j - 2] + 1);
                d[i, j] = value;
                rowBest = Math.Min(rowBest, value);
            }
            if (rowBest > max) return int.MaxValue;
        }
        return d[a.Length, b.Length];
    }
}
