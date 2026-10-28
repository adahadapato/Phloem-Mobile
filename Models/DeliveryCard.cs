namespace Phloem.Models;

/// <summary>One card in "My deliveries": a food order or a parcel the courier has to take somewhere.</summary>
public sealed class DeliveryCard
{
    public required DeliveryJobDto Job { get; init; }

    public DeliveryKind Kind => Job.Kind;
    public string Reference => Job.Reference;
    public bool IsParcel => Job.Kind == DeliveryKind.Parcel;

    /// <summary>"🍲 Food order · PH-261028-482193".</summary>
    public string Title => (IsParcel ? "📦  Parcel · " : "🍲  Food order · ") + Job.Reference;

    /// <summary>"Collect from Mama Put".</summary>
    public string CollectText => $"Collect from {Job.CollectFrom}";
    public bool HasCollectPhone => !string.IsNullOrWhiteSpace(Job.CollectPhone);

    public string RecipientText => Job.RecipientName;
    public bool HasRecipientPhone => !string.IsNullOrWhiteSpace(Job.RecipientPhone);
    public string RecipientPhoneText => Job.RecipientPhone ?? string.Empty;
    public string AddressText => Job.Address;
    public string ItemsText => Job.ItemsSummary;

    /// <summary>To deliver: "Deliver: As soon as possible · given to you 14:05". Done: "Left at the front door · 14:32".</summary>
    public string WhenText => Job.DeliveredAt is { } done
        ? $"{Job.HandoverSummary ?? "Delivered"} · {done.ToLocalTime():d MMM, HH:mm}"
        : (Job.DeliveryWindow is { } window ? $"Deliver: {DeliveryTimeOption.TextOf(window)}" : "Deliver today") +
          (Job.AssignedAt is { } at ? $" · given to you {at.ToLocalTime():HH:mm}" : string.Empty);

    public bool IsDone => Job.DeliveredAt is not null;
    public bool IsToDo => !IsDone;
}
