using Phloem.Settings;

namespace Phloem.Models;

/// <summary>
/// The "Proof of delivery" card (order and parcel pages): the courier's photo, how it was handed over, their note and
/// who delivered it when.
/// </summary>
public sealed class DeliveryProofView
{
    /// <summary>The photo (a link that works for about 15 minutes; the page reloads it each time it opens).</summary>
    public required string PhotoUrl { get; init; }

    /// <summary>"Left at the front door", "Handed to Ama".</summary>
    public required string Summary { get; init; }

    /// <summary>The courier's note, if any.</summary>
    public string? Note { get; init; }

    public bool HasNote => !string.IsNullOrWhiteSpace(Note);

    /// <summary>"Delivered 28 Oct, 14:32 by Kwame Asante".</summary>
    public required string MetaText { get; init; }

    /// <summary>Builds the card; null when there's no proof yet.</summary>
    public static DeliveryProofView? From(DeliveryProofDto? proof, ApiSettings api) => proof is null
        ? null
        : new DeliveryProofView
        {
            PhotoUrl = api.ResolveFileUrl(proof.PhotoUrl) ?? proof.PhotoUrl,
            Summary = proof.Summary,
            Note = proof.Note,
            MetaText = $"Delivered {proof.DeliveredAt.ToLocalTime():d MMM, HH:mm}" +
                       (string.IsNullOrWhiteSpace(proof.CourierName) ? string.Empty : $" by {proof.CourierName}")
        };
}
