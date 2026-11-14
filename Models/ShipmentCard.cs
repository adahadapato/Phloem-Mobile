using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Models;

/// <summary>
/// Words and labels for parcels, in one place so every screen says the same thing
/// (e.g. ReceivedAtHub → "At the hub · ready to pay").
/// </summary>
public static class ShipmentText
{
    /// <summary>What the status means to people.</summary>
    public static string Status(ShipmentStatus status) => status switch
    {
        ShipmentStatus.Booked => "Booked · drop it at the hub",
        ShipmentStatus.ReceivedAtHub => "At the hub · ready to pay",
        ShipmentStatus.Paid => "Paid · waiting to leave",
        ShipmentStatus.InTransit => "In transit",
        ShipmentStatus.ArrivedInDestination => "Arrived in destination",
        ShipmentStatus.OutForDelivery => "Out for delivery",
        ShipmentStatus.Delivered => "Delivered",
        ShipmentStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };

    /// <summary>Short status for chips and timelines.</summary>
    public static string Step(ShipmentStatus status) => status switch
    {
        ShipmentStatus.Booked => "Booked",
        ShipmentStatus.ReceivedAtHub => "Received at hub",
        ShipmentStatus.Paid => "Paid",
        ShipmentStatus.InTransit => "In transit",
        ShipmentStatus.ArrivedInDestination => "Arrived",
        ShipmentStatus.OutForDelivery => "Out for delivery",
        ShipmentStatus.Delivered => "Delivered",
        ShipmentStatus.Cancelled => "Cancelled",
        _ => status.ToString()
    };

    /// <summary>The staff's next journey step after this status; null when there's none (or staff can't move it).</summary>
    public static ShipmentStatus? NextJourneyStep(ShipmentStatus status) => status switch
    {
        ShipmentStatus.Paid => ShipmentStatus.InTransit,
        ShipmentStatus.InTransit => ShipmentStatus.ArrivedInDestination,
        ShipmentStatus.ArrivedInDestination => ShipmentStatus.OutForDelivery,
        ShipmentStatus.OutForDelivery => ShipmentStatus.Delivered,
        _ => null
    };

    public static string Speed(DeliverySpeed speed) => speed.ToString();

    public static string Category(ShipmentItemCategory category) => category switch
    {
        ShipmentItemCategory.PackagedFood => "Packaged food",
        _ => category.ToString()
    };

    /// <summary>"🇬🇭 Ghana → 🇬🇧 United Kingdom".</summary>
    public static string Route(Country from, Country to) => $"{CountryName(from)} → {CountryName(to)}";

    /// <summary>"🇬🇭 Ghana".</summary>
    public static string CountryName(Country country)
    {
        var option = CountryOption.All.FirstOrDefault(c => c.ApiCountry == country);
        return option is null ? country.ToString() : $"{option.FlagEmoji} {option.Name}";
    }

    /// <summary>"GH₵120.00".</summary>
    public static string Money(decimal amount, Currency currency) =>
        $"{Phloem.Services.OrderService.SymbolOf(currency)}{amount:N2}";

    /// <summary>"5–7 days", or "3 days".</summary>
    public static string Days(int min, int max) => min == max ? $"{min} day(s)" : $"{min}–{max} days";

    /// <summary>True for statuses shown in red.</summary>
    public static bool IsBad(ShipmentStatus status) => status == ShipmentStatus.Cancelled;

    /// <summary>True when something is waiting on the sender (drop it off, or pay).</summary>
    public static bool NeedsSender(ShipmentStatus status) =>
        status is ShipmentStatus.Booked or ShipmentStatus.ReceivedAtHub;
}

/// <summary>One parcel card (My shipments, and the admin / staff list).</summary>
public sealed class ShipmentCard
{
    public required string TrackingNumber { get; init; }
    public required ShipmentStatus Status { get; init; }
    public string RouteText { get; init; } = string.Empty;

    /// <summary>"To Ama Mensah" (sender's list) or "Kwame B. → Ama M." (staff list).</summary>
    public string PeopleText { get; init; } = string.Empty;

    /// <summary>"Express · 3 item(s) · 4.5 kg".</summary>
    public string DetailsText { get; init; } = string.Empty;

    /// <summary>"GH₵120.00" or "about GH₵120.00" (before it's weighed).</summary>
    public string PriceText { get; init; } = string.Empty;
    public DateTime BookedAt { get; init; }

    /// <summary>"Ready to collect" when it waits at our hub for the recipient; otherwise the status.</summary>
    public bool IsReadyToCollect { get; init; }

    public string StatusText => IsReadyToCollect ? "Ready to collect" : ShipmentText.Status(Status);
    public bool IsCancelled => ShipmentText.IsBad(Status);

    /// <summary>Highlighted when the sender must act (drop it off, or pay).</summary>
    public bool NeedsAction { get; init; }
    public string BookedAtText => BookedAt.ToString("d MMM yyyy");

    /// <summary>A card from "My shipments".</summary>
    public static ShipmentCard From(ShipmentSummaryDto s) => new()
    {
        TrackingNumber = s.TrackingNumber,
        Status = s.Status,
        RouteText = ShipmentText.Route(s.FromCountry, s.ToCountry),
        PeopleText = $"To {s.RecipientName}",
        DetailsText = $"{ShipmentText.Speed(s.Speed)} · {s.BoxCount} box{(s.BoxCount == 1 ? "" : "es")} · {s.ItemCount} item(s)",
        PriceText = (s.IsPriceFinal ? string.Empty : "about ") + ShipmentText.Money(s.Price, s.Currency),
        BookedAt = s.BookedAt.ToLocalTime(),
        NeedsAction = ShipmentText.NeedsSender(s.Status),
        IsReadyToCollect = s.IsReadyToCollect
    };

    /// <summary>A card from the staff / admin list.</summary>
    public static ShipmentCard From(StaffShipmentSummaryDto s) => new()
    {
        TrackingNumber = s.TrackingNumber,
        Status = s.Status,
        RouteText = ShipmentText.Route(s.FromCountry, s.ToCountry),
        PeopleText = $"{s.SenderName} → {s.RecipientName}",
        DetailsText = $"{ShipmentText.Speed(s.Speed)} · {s.BoxCount} box{(s.BoxCount == 1 ? "" : "es")} · " +
                      (s.WeighedWeightKg is { } w ? $"{w:0.##} kg weighed" : $"~{s.EstimatedWeightKg:0.##} kg") +
                      (s.IsFragile ? " · Fragile" : string.Empty) +
                      (s.HandOver == ParcelHandOver.HomePickup ? $" · Pickup in {s.PickupCity}" : string.Empty) +
                      (s.Delivery == ParcelDelivery.CollectFromHub ? " · Collects from hub" : string.Empty),
        PriceText = (s.WeighedWeightKg is null ? "about " : string.Empty) + ShipmentText.Money(s.Price, s.Currency),
        BookedAt = s.BookedAt.ToLocalTime(),
        NeedsAction = s.Status is ShipmentStatus.Booked or ShipmentStatus.Paid
                                  or ShipmentStatus.InTransit or ShipmentStatus.ArrivedInDestination
                                  or ShipmentStatus.OutForDelivery
    };
}

/// <summary>One step on a parcel's timeline (Shipment details).</summary>
public sealed class ShipmentStep
{
    public required string Title { get; init; }
    public required string WhenText { get; init; }
    public string? Note { get; init; }
    public bool HasNote => !string.IsNullOrWhiteSpace(Note);
    public bool IsCancelled { get; init; }

    public static ShipmentStep From(ShipmentEventDto e) => new()
    {
        Title = ShipmentText.Step(e.Status),
        WhenText = e.OccurredAt.ToLocalTime().ToString("d MMM yyyy, HH:mm"),
        Note = e.Note,
        IsCancelled = e.Status == ShipmentStatus.Cancelled
    };
}

/// <summary>One item in a parcel (Shipment details).</summary>
public sealed class ShipmentItemLine
{
    public required string Title { get; init; }
    public required string Subtitle { get; init; }
    public required string ValueText { get; init; }

    public static ShipmentItemLine From(ShipmentItemDto item, Currency currency) => new()
    {
        Title = $"{item.Description} ×{item.Quantity}",
        Subtitle = ShipmentText.Category(item.Category),
        ValueText = ShipmentText.Money(item.TotalDeclaredValue, currency)
    };
}
