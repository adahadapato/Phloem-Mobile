namespace Phloem.Models;

/// <summary>One row in the full-screen State / LGA list.</summary>
/// <param name="Id">The state's or LGA's Id.</param>
/// <param name="Name">e.g. "Lagos", "Eti-Osa".</param>
/// <param name="Detail">Optional small text under the name.</param>
/// <param name="IsSelected">The one picked before (shown with a tick).</param>
public sealed record LocationOption(Guid Id, string Name, string? Detail, bool IsSelected = false)
{
    public bool HasDetail => !string.IsNullOrEmpty(Detail);
}
