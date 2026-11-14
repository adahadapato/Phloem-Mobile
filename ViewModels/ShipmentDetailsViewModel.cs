using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using Phloem.Helpers;
using Phloem.Models;
using Phloem.Services;
using Phloem.Services.Api;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;
using Phloem.Settings;

namespace Phloem.ViewModels;

/// <summary>
/// Shipment details (route "shipmentdetails?tracking=…[&amp;staff=true]"): one parcel with its status, timeline,
/// recipient (with LGA), boxes (declared and measured), fragile / pickup / hub collection, items, weight and the price
/// part by part.
/// <list type="bullet">
///   <item><description><b>Sender:</b> Pay (test payment, once weighed) and Cancel (before it leaves).</description></item>
///   <item><description><b>Staff</b> (admin / ShipmentManager, opened from All shipments): Received at hub (weigh AND
///   measure every box), the next journey step with an optional note, and "Out for delivery" with a courier (or change
///   the courier). For hub collection, "out for delivery" means "ready to collect" and the staff member handing it
///   over records it like a courier. The courier marks it delivered with a photo, shown here to everyone.</description></item>
/// </list>
/// The same screen for both, so they always show a parcel the same way.
/// </summary>
public partial class ShipmentDetailsViewModel : ObservableObject, IQueryAttributable
{
    private readonly ShipmentService _shipments;
    private readonly ApiSettings _api;

    public ShipmentDetailsViewModel(ShipmentService shipments, IOptions<ApiSettings> api, ReviewService reviews)
    {
        _shipments = shipments;
        _api = api.Value;
        Rate = new RateCardViewModel(reviews);
    }

    /// <summary>"How did we do?" once delivered (senders only, not staff).</summary>
    public RateCardViewModel Rate { get; }

    private string _trackingNumber = string.Empty;

    /// <summary>True when opened by staff (All shipments).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSender))]
    public partial bool IsStaff { get; set; }

    public bool IsSender => !IsStaff;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasShipment), nameof(StatusText), nameof(IsCancelled), nameof(RouteText),
                              nameof(SpeedText), nameof(ArrivalText), nameof(RecipientText), nameof(SenderText),
                              nameof(WeightText), nameof(PriceText), nameof(PriceCaption), nameof(DropOffText),
                              nameof(ShowDropOff), nameof(CanPay), nameof(CanCancel), nameof(CanReceive),
                              nameof(NextStepText), nameof(CanMoveOn), nameof(RefundText), nameof(NeedsRefund),
                              nameof(CourierText), nameof(HasCourier), nameof(CanChangeCourier), nameof(Proof),
                              nameof(BoxesText), nameof(ChargedAsText), nameof(ExtrasText), nameof(HasExtras),
                              nameof(ShowPickup), nameof(PickupText), nameof(ShowReadyToCollect),
                              nameof(ReadyToCollectText), nameof(HasBreakdown))]
    public partial ShipmentDetailsDto? Shipment { get; set; }

    public ObservableCollection<ShipmentStep> Timeline { get; } = new();
    public ObservableCollection<ShipmentItemLine> Items { get; } = new();

    public bool HasShipment => Shipment is not null;
    public string TrackingNumber => _trackingNumber;
    public string StatusText => Shipment is { } s ? ShipmentText.Status(s.Status) : string.Empty;
    public bool IsCancelled => Shipment?.Status == ShipmentStatus.Cancelled;
    public string RouteText => Shipment is { } s ? ShipmentText.Route(s.FromCountry, s.ToCountry) : string.Empty;
    public string SpeedText => Shipment is { } s ? ShipmentText.Speed(s.Speed) : string.Empty;

    public string ArrivalText => Shipment switch
    {
        { DeliveredAt: { } at } => $"Delivered {at.ToLocalTime():d MMM yyyy}",
        { EstimatedArrivalFrom: { } from, EstimatedArrivalTo: { } to } =>
            from == to ? $"Expected {from:d MMM}" : $"Expected {from:d MMM} – {to:d MMM}",
        _ => "Expected date set once it leaves"
    };

    public string RecipientText => Shipment is { } s
        ? $"{s.RecipientName} · {s.RecipientPhone}\n" +
          string.Join(", ", new[] { s.RecipientAddressLine, s.RecipientCity,
                                    s.RecipientDistrictName is null ? null : $"{s.RecipientDistrictName} LGA",
                                    s.RecipientRegionOrPostcode }.Where(x => !string.IsNullOrWhiteSpace(x))) +
          (s.Delivery == ParcelDelivery.CollectFromHub ? "\nCollects from our hub" : string.Empty)
        : string.Empty;

    public string SenderText => Shipment is { } s ? $"{s.SenderName} · {s.SenderPhone}" : string.Empty;

    public string WeightText => Shipment is { } s
        ? s.WeighedWeightKg is { } w ? $"{w:0.##} kg (weighed)" : $"about {s.EstimatedWeightKg:0.##} kg (not weighed yet)"
        : string.Empty;

    /// <summary>"2 × 8 kg · 60 × 40 × 40 cm (counts as 39 kg)", one line per box line (measured if weighed).</summary>
    public string BoxesText
    {
        get
        {
            if (Shipment is not { } s) return string.Empty;
            var boxes = s.MeasuredBoxes is { Count: > 0 } m ? m : s.DeclaredBoxes ?? [];
            if (boxes.Count == 0) return "One parcel (booked before box sizes)";
            var lines = boxes.Select(b =>
                $"{b.Quantity} × {ParcelMath.Kg(b.WeightKg)}" +
                (b.LengthCm is null ? " · size not given" : $" · {b.LengthCm:0.#} × {b.WidthCm:0.#} × {b.HeightCm:0.#} cm") +
                $" (counts as {ParcelMath.Kg(b.ChargeableWeightKg)})");
            var heading = s.MeasuredBoxes is { Count: > 0 } ? "Measured at the hub:\n" : string.Empty;
            return heading + string.Join("\n", lines);
        }
    }

    /// <summary>"Charged as 39 kg (weight or size, whichever is higher)".</summary>
    public string ChargedAsText => Shipment is { } s && (s.ChargeableWeightKg ?? s.EstimatedChargeableWeightKg) is var kg and > 0m
        ? $"Charged as {ParcelMath.Kg(kg)} (weight or size, whichever is higher)"
        : string.Empty;

    /// <summary>"Fragile · Home pickup · Recipient collects from our hub".</summary>
    public string ExtrasText => Shipment is { } s
        ? string.Join(" · ", new[]
          {
              s.IsFragile ? "Fragile" : null,
              s.HandOver == ParcelHandOver.HomePickup ? "Home pickup" : null,
              s.Delivery == ParcelDelivery.CollectFromHub ? "Recipient collects from our hub" : null
          }.Where(x => x is not null))
        : string.Empty;

    public bool HasExtras => ExtrasText.Length > 0;

    /// <summary>The price, part by part (only parcels booked since the breakdown existed).</summary>
    public ObservableCollection<PriceLine> Breakdown { get; } = new();
    public bool HasBreakdown => Breakdown.Count > 0;

    public bool ShowPickup => Shipment is { HandOver: ParcelHandOver.HomePickup, Status: ShipmentStatus.Booked };
    public string PickupText => Shipment is { } s
        ? $"{s.PickupAddressLine}, {s.PickupCity}, {s.PickupRegionOrPostcode}" +
          (string.IsNullOrWhiteSpace(s.PickupNotes) ? string.Empty : $"\n“{s.PickupNotes}”")
        : string.Empty;

    public bool ShowReadyToCollect => Shipment?.IsReadyToCollect == true;
    public string ReadyToCollectText => Shipment is { } s
        ? $"{s.CollectionHubAddress}\n{s.CollectionHubHours}\n{s.RecipientName} can collect it with photo ID and the tracking number."
        : string.Empty;

    public string PriceText => Shipment is { } s ? ShipmentText.Money(s.FinalPrice ?? s.EstimatedPrice, s.Currency) : string.Empty;
    public string PriceCaption => Shipment?.FinalPrice is null ? "Estimated price" : "Final price";

    public bool ShowDropOff => Shipment is { Status: ShipmentStatus.Booked, DropOffAddress: not null, HandOver: ParcelHandOver.HubDropOff };
    public string DropOffText => Shipment is { } s ? $"{s.DropOffAddress}\n{s.DropOffHours}" : string.Empty;

    public bool NeedsRefund => Shipment?.NeedsRefund == true;
    public string RefundText => "Paid, then cancelled: the payment will be refunded.";

    // ---------- Sender actions ----------
    public bool CanPay => IsSender && Shipment?.CanPay == true;
    public bool CanCancel => IsSender && Shipment?.CanCancel == true;

    // ---------- Staff actions ----------
    public bool CanReceive => IsStaff && Shipment?.Status == ShipmentStatus.Booked && !IsMeasuring;

    /// <summary>The weigh-and-measure form is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanReceive))]
    public partial bool IsMeasuring { get; set; }

    /// <summary>Each box as staff weigh and measure it (starts from what the sender declared).</summary>
    public ObservableCollection<ParcelBoxDraft> HubBoxes { get; } = new();
    /// <summary>Staff move it on up to "Out for delivery"; only the courier can mark it delivered (with a photo).</summary>
    public bool CanMoveOn => IsStaff && Shipment is { } s && ShipmentText.NextJourneyStep(s.Status) is { } next
                             && next != ShipmentStatus.Delivered;

    public bool CanChangeCourier => IsStaff && Shipment?.Status == ShipmentStatus.OutForDelivery;

    /// <summary>"🛵 Kwame Asante is delivering it" / "Delivered by …".</summary>
    public string CourierText => Shipment switch
    {
        { CourierName: null } or null => string.Empty,
        { Status: ShipmentStatus.Delivered, Delivery: ParcelDelivery.CollectFromHub } s => $"📦  Collected from our hub · handed over by {s.CourierName}",
        { Status: ShipmentStatus.Delivered } s => $"🛵  Delivered by {s.CourierName}",
        { IsReadyToCollect: true } s => $"📦  Ready to collect at our hub · {s.CourierName} hands it over",
        var s => $"🛵  {s.CourierName} is delivering it"
    };

    public bool HasCourier => CourierText.Length > 0;

    /// <summary>The courier's photo once delivered; null before.</summary>
    public DeliveryProofView? Proof => DeliveryProofView.From(Shipment?.DeliveryProof, _api);
    public string NextStepText => Shipment is { } s && ShipmentText.NextJourneyStep(s.Status) is { } next
        ? next == ShipmentStatus.OutForDelivery && s.Delivery == ParcelDelivery.CollectFromHub
            ? "Mark as: Ready to collect at our hub"
            : $"Mark as: {ShipmentText.Step(next)}"
        : string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    // ═════════════════════════════ Loading ═════════════════════════════

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _trackingNumber = query.TryGetValue("tracking", out var t) ? Uri.UnescapeDataString(t?.ToString() ?? "") : "";
        IsStaff = query.TryGetValue("staff", out var s) && string.Equals(s?.ToString(), "true",
                                                                           StringComparison.OrdinalIgnoreCase);
        OnPropertyChanged(nameof(TrackingNumber));
    }

    /// <summary>Called each time the page appears: the parcel as it is now.</summary>
    public async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(_trackingNumber)) return;

        IsBusy = true;
        Message = Shipment is null ? "Loading the parcel…" : string.Empty;
        try
        {
            var result = IsStaff
                ? await _shipments.GetAnyAsync(_trackingNumber)
                : await _shipments.GetMineAsync(_trackingNumber);
            Show(result);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Show(ApiResult<ShipmentDetailsDto> result)
    {
        if (result.IsFailure)
        {
            Message = result.Error!.DisplayMessage;
            return;
        }

        var s = result.Value!;
        Shipment = s;
        Message = string.Empty;
        _ = Rate.LoadAsync(ReviewKind.Parcel, s.TrackingNumber, !IsStaff && s.Status == ShipmentStatus.Delivered,
                           "How did we do with your parcel?");

        Timeline.Clear();
        foreach (var e in s.Timeline.OrderByDescending(e => e.OccurredAt)) Timeline.Add(ShipmentStep.From(e));

        Items.Clear();
        foreach (var i in s.Items) Items.Add(ShipmentItemLine.From(i, s.Currency));

        Breakdown.Clear();
        if (s.FragileCharge + s.HomePickupCharge + s.DoorDeliveryCharge > 0)
        {
            Breakdown.Add(new PriceLine("Shipping", ShipmentText.Money(s.FreightCharge, s.Currency)));
            if (s.FragileCharge > 0) Breakdown.Add(new PriceLine("Fragile handling", ShipmentText.Money(s.FragileCharge, s.Currency)));
            if (s.HomePickupCharge > 0) Breakdown.Add(new PriceLine("Home pickup", ShipmentText.Money(s.HomePickupCharge, s.Currency)));
            if (s.DoorDeliveryCharge > 0) Breakdown.Add(new PriceLine("Door delivery", ShipmentText.Money(s.DoorDeliveryCharge, s.Currency)));
        }
        OnPropertyChanged(nameof(HasBreakdown));
    }

    // ═════════════════════════════ Sender ═════════════════════════════

    /// <summary>Pay the final price (a TEST payment for now: no money is taken).</summary>
    [RelayCommand]
    private async Task Pay()
    {
        if (IsBusy || Shipment is null) return;

        var ok = await Shell.Current.DisplayAlertAsync("Pay for this parcel?",
            $"{PriceText} · {_trackingNumber}\nThis is a TEST payment: no money will be taken.", "Pay", "Not now");
        if (!ok) return;

        IsBusy = true;
        try
        {
            var result = await _shipments.PayWithTestPaymentAsync(_trackingNumber);
            if (result.IsFailure)
            {
                await Shell.Current.DisplayAlertAsync("Couldn't pay", result.Error!.DisplayMessage, "OK");
                return;
            }

            Show(result);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Cancel()
    {
        if (IsBusy || Shipment is null) return;

        var refund = Shipment.Status == ShipmentStatus.Paid ? "\nYour payment will be refunded." : string.Empty;
        var ok = await Shell.Current.DisplayAlertAsync("Cancel this parcel?", $"{_trackingNumber}{refund}",
                                                       "Cancel parcel", "Keep it");
        if (!ok) return;

        await RunAsync(() => _shipments.CancelAsync(_trackingNumber), "Couldn't cancel");
    }

    // ═════════════════════════════ Staff ═════════════════════════════

    /// <summary>
    /// Received at the hub: opens the weigh-and-measure form, one line per box line the sender declared (staff fill in
    /// the real weight and size, and can add or remove lines).
    /// </summary>
    [RelayCommand]
    private void Receive()
    {
        if (IsBusy || Shipment is not { } s) return;

        HubBoxes.Clear();
        var declared = s.DeclaredBoxes ?? [];
        if (declared.Count == 0) AddHubBox();
        else foreach (var box in declared) HubBoxes.Add(ParcelBoxDraft.FromDeclared(box, s.VolumeDivisor));
        RenumberHubBoxes();
        IsMeasuring = true;
    }

    [RelayCommand]
    private void AddHubBox()
    {
        if (HubBoxes.Count >= ParcelMath.MaxLines) return;
        HubBoxes.Add(new ParcelBoxDraft { Divisor = Shipment?.VolumeDivisor ?? 5000 });
        RenumberHubBoxes();
    }

    [RelayCommand]
    private void RemoveHubBox(ParcelBoxDraft box)
    {
        if (HubBoxes.Count <= 1) return;
        HubBoxes.Remove(box);
        RenumberHubBoxes();
    }

    private void RenumberHubBoxes()
    {
        for (var i = 0; i < HubBoxes.Count; i++)
        {
            HubBoxes[i].Title = $"Box {i + 1}";
            HubBoxes[i].CanRemove = HubBoxes.Count > 1;
        }
    }

    [RelayCommand]
    private void CancelReceive() => IsMeasuring = false;

    /// <summary>Saves the measured boxes → the final price; the sender is emailed to pay.</summary>
    [RelayCommand]
    private async Task SaveReceive()
    {
        if (IsBusy || Shipment is null) return;

        if (HubBoxes.Any(b => !b.IsValid || !b.HasSize))
        {
            await Shell.Current.DisplayAlertAsync("Weigh and measure",
                "For every box, enter the weight (kg) and the length, width and height (cm).", "OK");
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _shipments.ReceiveAtHubAsync(_trackingNumber, HubBoxes.Select(b => b.ToInput()).ToList());
            if (result.IsFailure)
            {
                await Shell.Current.DisplayAlertAsync("Couldn't save", result.Error!.DisplayMessage, "OK");
                return;
            }

            IsMeasuring = false;
            await Shell.Current.DisplayAlertAsync("Received at hub",
                $"Final price {ShipmentText.Money(result.Value, Shipment.Currency)}. {Shipment.SenderName} has been emailed to pay.", "OK");
        }
        finally
        {
            IsBusy = false;
        }

        await LoadAsync();
    }

    /// <summary>
    /// The next journey step (in transit → arrived), with an optional note; "Out for delivery" also needs a courier.
    /// </summary>
    [RelayCommand]
    private async Task MoveOn()
    {
        if (IsBusy || Shipment is null || ShipmentText.NextJourneyStep(Shipment.Status) is not { } next) return;

        if (next == ShipmentStatus.OutForDelivery)
        {
            var collecting = Shipment.Delivery == ParcelDelivery.CollectFromHub;
            var courier = await ChooseCourierAsync(collecting ? "Who hands it over at the hub?" : "Who's delivering it?");
            if (courier is null) return;

            var outNote = await Shell.Current.DisplayPromptAsync(collecting ? "Ready to collect" : "Out for delivery",
                collecting
                    ? "Optional note for the timeline (blank = \"Ready to collect at our hub\"):"
                    : $"Optional note for the timeline (blank = \"With our courier, {courier.Name}\"):",
                "Save", "Cancel", placeholder: "Note (optional)");
            if (outNote is null) return;

            await RunAsync(() => _shipments.SendOutForDeliveryAsync(_trackingNumber, courier.Id, outNote),
                           "Couldn't update");
            return;
        }

        var note = await Shell.Current.DisplayPromptAsync(ShipmentText.Step(next),
            "Optional note for the timeline (e.g. \"Flight BA078\", \"Signed for by K. Boateng\"):",
            "Save", "Cancel", placeholder: "Note (optional)");
        if (note is null) return;

        await RunAsync(() => _shipments.UpdateProgressAsync(_trackingNumber, next, note), "Couldn't update");
    }

    /// <summary>Give a parcel that's out for delivery to another courier.</summary>
    [RelayCommand]
    private async Task ChangeCourier()
    {
        if (IsBusy || Shipment is null) return;
        var courier = await ChooseCourierAsync("Who's delivering it now?");
        if (courier is null) return;

        await RunAsync(() => _shipments.ChangeCourierAsync(_trackingNumber, courier.Id), "Couldn't change courier");
    }

    /// <summary>The couriers in the destination country; null when cancelled or there are none.</summary>
    private async Task<CourierOptionDto?> ChooseCourierAsync(string title)
    {
        var result = await _shipments.GetCouriersAsync(Shipment!.ToCountry);
        if (result.IsFailure)
        {
            await Shell.Current.DisplayAlertAsync("Couriers", result.Error!.DisplayMessage, "OK");
            return null;
        }

        var couriers = result.Value!;
        if (couriers.Count == 0)
        {
            await Shell.Current.DisplayAlertAsync("No couriers yet",
                "Nobody has the Courier role in this country yet. Give it to someone on Admin → Users.", "OK");
            return null;
        }

        var labels = couriers.Select(c => c.Name).ToArray();
        var picked = await Shell.Current.DisplayActionSheetAsync(title, "Cancel", null, labels);
        var index = Array.IndexOf(labels, picked);
        return index < 0 ? null : couriers[index];
    }

    /// <summary>Runs an action, shows the server's message if it fails, then reloads the parcel.</summary>
    private async Task RunAsync(Func<Task<ApiResult>> action, string failTitle)
    {
        IsBusy = true;
        try
        {
            var result = await action();
            if (result.IsFailure)
            {
                await Shell.Current.DisplayAlertAsync(failTitle, result.Error!.DisplayMessage, "OK");
                return;
            }
        }
        finally
        {
            IsBusy = false;
        }

        await LoadAsync();
    }
}
