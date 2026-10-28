using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Options;
using Phloem.Models;
using Phloem.Services;
using Phloem.Services.Api;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;
using Phloem.Settings;

namespace Phloem.ViewModels;

/// <summary>
/// Shipment details (route "shipmentdetails?tracking=…[&amp;staff=true]"): one parcel with its status, timeline,
/// recipient, items, weight and price.
/// <list type="bullet">
///   <item><description><b>Sender:</b> Pay (test payment, once weighed) and Cancel (before it leaves).</description></item>
///   <item><description><b>Staff</b> (admin / ShipmentManager, opened from All shipments): Received at hub (enter the
///   weight), the next journey step with an optional note, and "Out for delivery" with a courier (or change the
///   courier). The courier marks it delivered with a photo, shown here to everyone.</description></item>
/// </list>
/// The same screen for both, so they always show a parcel the same way.
/// </summary>
public partial class ShipmentDetailsViewModel : ObservableObject, IQueryAttributable
{
    private readonly ShipmentService _shipments;
    private readonly ApiSettings _api;

    public ShipmentDetailsViewModel(ShipmentService shipments, IOptions<ApiSettings> api)
    {
        _shipments = shipments;
        _api = api.Value;
    }

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
                              nameof(CourierText), nameof(HasCourier), nameof(CanChangeCourier), nameof(Proof))]
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
        ? $"{s.RecipientName} · {s.RecipientPhone}\n{s.RecipientAddressLine}, {s.RecipientCity}, {s.RecipientRegionOrPostcode}"
        : string.Empty;

    public string SenderText => Shipment is { } s ? $"{s.SenderName} · {s.SenderPhone}" : string.Empty;

    public string WeightText => Shipment is { } s
        ? s.WeighedWeightKg is { } w ? $"{w:0.##} kg (weighed)" : $"about {s.EstimatedWeightKg:0.##} kg (not weighed yet)"
        : string.Empty;

    public string PriceText => Shipment is { } s ? ShipmentText.Money(s.FinalPrice ?? s.EstimatedPrice, s.Currency) : string.Empty;
    public string PriceCaption => Shipment?.FinalPrice is null ? "Estimated price" : "Final price";

    public bool ShowDropOff => Shipment is { Status: ShipmentStatus.Booked, DropOffAddress: not null };
    public string DropOffText => Shipment is { } s ? $"{s.DropOffAddress}\n{s.DropOffHours}" : string.Empty;

    public bool NeedsRefund => Shipment?.NeedsRefund == true;
    public string RefundText => "Paid, then cancelled: the payment will be refunded.";

    // ---------- Sender actions ----------
    public bool CanPay => IsSender && Shipment?.CanPay == true;
    public bool CanCancel => IsSender && Shipment?.CanCancel == true;

    // ---------- Staff actions ----------
    public bool CanReceive => IsStaff && Shipment?.Status == ShipmentStatus.Booked;
    /// <summary>Staff move it on up to "Out for delivery"; only the courier can mark it delivered (with a photo).</summary>
    public bool CanMoveOn => IsStaff && Shipment is { } s && ShipmentText.NextJourneyStep(s.Status) is { } next
                             && next != ShipmentStatus.Delivered;

    public bool CanChangeCourier => IsStaff && Shipment?.Status == ShipmentStatus.OutForDelivery;

    /// <summary>"🛵 Kwame Asante is delivering it" / "Delivered by …".</summary>
    public string CourierText => Shipment switch
    {
        { CourierName: null } or null => string.Empty,
        { Status: ShipmentStatus.Delivered } s => $"🛵  Delivered by {s.CourierName}",
        var s => $"🛵  {s.CourierName} is delivering it"
    };

    public bool HasCourier => CourierText.Length > 0;

    /// <summary>The courier's photo once delivered; null before.</summary>
    public DeliveryProofView? Proof => DeliveryProofView.From(Shipment?.DeliveryProof, _api);
    public string NextStepText => Shipment is { } s && ShipmentText.NextJourneyStep(s.Status) is { } next
        ? $"Mark as: {ShipmentText.Step(next)}"
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

        Timeline.Clear();
        foreach (var e in s.Timeline.OrderByDescending(e => e.OccurredAt)) Timeline.Add(ShipmentStep.From(e));

        Items.Clear();
        foreach (var i in s.Items) Items.Add(ShipmentItemLine.From(i, s.Currency));
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

    /// <summary>Dropped off at the hub: enter the weight → sets the final price.</summary>
    [RelayCommand]
    private async Task Receive()
    {
        if (IsBusy || Shipment is null) return;

        var text = await Shell.Current.DisplayPromptAsync("Received at hub",
            $"Weight on the scale, in kg (the sender estimated {Shipment.EstimatedWeightKg:0.##} kg):",
            "Save", "Cancel", placeholder: "e.g. 4.6", keyboard: Keyboard.Numeric);
        if (text is null) return;

        if (InternationalDeliveryViewModel.ParseDecimal(text) is not { } kg || kg <= 0)
        {
            await Shell.Current.DisplayAlertAsync("Weight", "Please enter the weight in kg, e.g. 4.6.", "OK");
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _shipments.ReceiveAtHubAsync(_trackingNumber, kg);
            if (result.IsFailure)
            {
                await Shell.Current.DisplayAlertAsync("Couldn't save", result.Error!.DisplayMessage, "OK");
                return;
            }
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
            var courier = await ChooseCourierAsync("Who's delivering it?");
            if (courier is null) return;

            var outNote = await Shell.Current.DisplayPromptAsync("Out for delivery",
                $"Optional note for the timeline (blank = \"With our courier, {courier.Name}\"):",
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
