using Phloem.Settings;

namespace Phloem.Models;

/// <summary>
/// Door safety on the customer's order page and the sender's parcel page, while it's on its way: who is coming, the
/// door word to ask for before opening, and the handover PIN to give when taking it.
/// </summary>
public sealed class DoorCheckView
{
    public required CourierConcernKind Kind { get; init; }
    public required string Reference { get; init; }
    public required string DoorWord { get; init; }
    public required string HandoverPin { get; init; }
    public required string EmergencyNumber { get; init; }
    public string? CourierName { get; init; }
    public string? PhotoUrl { get; init; }
    public string? VehicleText { get; init; }
    public string? Plate { get; init; }

    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoUrl);
    public bool HasPlate => !string.IsNullOrWhiteSpace(Plate);
    public bool IsParcel => Kind == CourierConcernKind.Parcel;
    public string ComingText => CourierName is null ? "Your courier is on the way" : $"{CourierName} is bringing it";
    public string CheckText => HasPlate
        ? "1. Check the person looks like the photo and the number plate matches."
        : "1. Check the person looks like the photo.";

    /// <summary>The card; null when there's nothing to show.</summary>
    public static DoorCheckView? From(DoorCheckDto? check, CourierConcernKind kind, string reference, ApiSettings api)
    {
        if (check is null) return null;
        var c = check.Courier;
        var vehicle = c is null ? null
            : string.Join(" ", new[] { c.Colour, c.MobilityName?.ToLowerInvariant(), c.Make is null ? null : $"({c.Make})" }
                                .Where(p => !string.IsNullOrWhiteSpace(p)));
        return new DoorCheckView
        {
            Kind = kind,
            Reference = reference,
            DoorWord = check.DoorWord,
            HandoverPin = check.HandoverPin,
            EmergencyNumber = check.EmergencyNumber,
            CourierName = c?.FirstName,
            PhotoUrl = c?.PhotoUrl is null ? null : api.ResolveFileUrl(c.PhotoUrl) ?? c.PhotoUrl,
            VehicleText = string.IsNullOrWhiteSpace(vehicle) ? null : vehicle,
            Plate = c?.RegistrationNumber
        };
    }
}
