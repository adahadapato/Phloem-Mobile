using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Services;

namespace Phloem.ViewModels;

/// <summary>One "How do you get around?" choice.</summary>
public sealed partial class MobilityOption : ObservableObject
{
    public required MobilityType Value { get; init; }
    public required string Label { get; init; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>One document on "Become a courier": its number, a photo from the camera, and what staff said.</summary>
public sealed partial class CourierDocumentRow : ObservableObject
{
    public required CourierDocumentType Type { get; init; }
    public required string Name { get; init; }
    public required bool NeedsNumber { get; init; }
    public required string NumberHelp { get; init; }

    /// <summary>What the person types (filled with the number already given).</summary>
    [ObservableProperty]
    public partial string Number { get; set; } = string.Empty;

    /// <summary>The photo on screen: the one just taken, or a link to the one given.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhoto), nameof(ButtonText))]
    public partial string? PhotoSource { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatus))]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRejection))]
    public partial string? RejectionReason { get; set; }

    [ObservableProperty]
    public partial bool IsSending { get; set; }

    public bool HasPhoto => !string.IsNullOrEmpty(PhotoSource);
    public bool HasStatus => StatusText.Length > 0;
    public bool HasRejection => !string.IsNullOrWhiteSpace(RejectionReason);
    public string ButtonText => HasPhoto ? "📷  Take it again" : "📷  Take a photo";
    public string NumberPlaceholder => Type == CourierDocumentType.DrivingLicence ? "Licence number" : "Number";
}

/// <summary>
/// "Become a courier" (route "becomecourier"): 1. how you get around, with two LIVE photos of the vehicle (front
/// with the number plate, and side; no gallery), checked by the AI at once; 2. the documents your country and vehicle
/// need (passport photo, NIN / Ghana Card / photo ID, driving licence), each photographed with its number; 3. send
/// it to Phloem staff. Same checks for every courier, including sellers who deliver their own orders.
/// </summary>
public partial class BecomeCourierViewModel : ObservableObject
{
    private readonly CourierService _couriers;
    private FileResult? _front;
    private FileResult? _side;

    public BecomeCourierViewModel(CourierService couriers)
    {
        _couriers = couriers;
        Mobilities =
        [
            new MobilityOption { Value = MobilityType.Motorbike, Label = "🏍  Motorbike" },
            new MobilityOption { Value = MobilityType.Keke, Label = "🛺  Keke" },
            new MobilityOption { Value = MobilityType.Car, Label = "🚗  Car" },
            new MobilityOption { Value = MobilityType.Van, Label = "🚐  Van" },
            new MobilityOption { Value = MobilityType.Bicycle, Label = "🚲  Bicycle" },
            new MobilityOption { Value = MobilityType.EBike, Label = "⚡  E-bike" },
            new MobilityOption { Value = MobilityType.OnFoot, Label = "🚶  On foot" }
        ];
    }

    public ObservableCollection<MobilityOption> Mobilities { get; }
    public ObservableCollection<CourierDocumentRow> Documents { get; } = [];
    public ObservableCollection<string> VehicleHints { get; } = [];
    public ObservableCollection<string> StillNeeded { get; } = [];

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string Message { get; set; } = string.Empty;

    public bool HasMessage => Message.Length > 0;

    // ───────────────────────────── Where it stands ─────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditable), nameof(StatusTitle), nameof(IsApproved), nameof(IsWaiting),
                              nameof(ShowSubmit))]
    public partial CourierRegistrationStatus Status { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDecisionNote))]
    public partial string? DecisionNote { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowSubmit))]
    public partial bool CanSubmit { get; set; }

    public bool HasDecisionNote => !string.IsNullOrWhiteSpace(DecisionNote);
    public bool IsEditable => Status is CourierRegistrationStatus.Draft or CourierRegistrationStatus.Rejected;
    public bool IsApproved => Status == CourierRegistrationStatus.Approved;
    public bool IsWaiting => Status == CourierRegistrationStatus.WaitingForStaff;
    public bool ShowSubmit => IsEditable;
    public bool HasStillNeeded => StillNeeded.Count > 0;

    public string StatusTitle => Status switch
    {
        CourierRegistrationStatus.WaitingForStaff => "⏳  Being checked",
        CourierRegistrationStatus.Approved => "✅  Approved courier",
        CourierRegistrationStatus.Rejected => "✏️  Please fix and send again",
        _ => "🛵  Become a courier"
    };

    // ───────────────────────────── 1. Vehicle ─────────────────────────────

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NeedsRegistration), nameof(NeedsPhotos))]
    public partial MobilityOption? SelectedMobility { get; set; }

    public bool NeedsRegistration => SelectedMobility?.Value is MobilityType.Motorbike or MobilityType.Keke
                                                            or MobilityType.Car or MobilityType.Van;

    public bool NeedsPhotos => SelectedMobility is not null && SelectedMobility.Value != MobilityType.OnFoot;

    [ObservableProperty]
    public partial string RegistrationNumber { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Make { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Colour { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFront))]
    public partial string? FrontPhoto { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSide))]
    public partial string? SidePhoto { get; set; }

    public bool HasFront => !string.IsNullOrEmpty(FrontPhoto);
    public bool HasSide => !string.IsNullOrEmpty(SidePhoto);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasVehicleStatus))]
    public partial string VehicleStatus { get; set; } = string.Empty;

    public bool HasVehicleStatus => VehicleStatus.Length > 0;
    public bool HasVehicleHints => VehicleHints.Count > 0;

    // ───────────────────────────── Loading ─────────────────────────────

    /// <summary>Loads the registration each time the screen appears.</summary>
    public async Task LoadAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            var result = await _couriers.GetMineAsync();
            if (result.IsFailure) { Message = result.Error!.DisplayMessage; return; }
            Show(result.Value!);
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void Show(MyCourierRegistrationDto r)
    {
        Status = r.Status;
        StatusMessage = r.Message;
        DecisionNote = r.DecisionNote;
        CanSubmit = r.CanSubmit;

        StillNeeded.Clear();
        foreach (var item in r.StillNeeded) StillNeeded.Add(item);
        OnPropertyChanged(nameof(HasStillNeeded));

        if (r.Vehicle is { } v) ShowVehicle(v);

        Documents.Clear();
        foreach (var d in r.Documents)
        {
            Documents.Add(new CourierDocumentRow
            {
                Type = d.Type,
                Name = d.Name,
                NeedsNumber = d.NeedsNumber,
                NumberHelp = d.NumberHelp,
                Number = d.Number ?? string.Empty,
                PhotoSource = d.FileUrl,
                StatusText = d.Status switch
                {
                    DocumentStatus.Accepted => "✅ Accepted",
                    DocumentStatus.Rejected => "✏️ Please give it again",
                    DocumentStatus.Pending => "✓ Given",
                    _ => string.Empty
                },
                RejectionReason = d.RejectionReason
            });
        }
    }

    private void ShowVehicle(MyVehicleDto v)
    {
        SelectMobilityValue(v.Mobility);
        RegistrationNumber = v.RegistrationNumber ?? string.Empty;
        Make = v.Make ?? string.Empty;
        Colour = v.Colour ?? string.Empty;
        _front = null;
        _side = null;
        FrontPhoto = v.FrontPhotoUrl;
        SidePhoto = v.SidePhotoUrl;
        VehicleStatus = v.Status switch
        {
            VehicleCheckStatus.Approved => "✅ Vehicle approved",
            VehicleCheckStatus.Rejected => $"✏️ Not approved: {v.StaffNote}",
            _ => v.Hints.Count == 0 ? "✓ Vehicle details sent" : "⚠ Please check these:"
        };
        VehicleHints.Clear();
        foreach (var hint in v.Hints) VehicleHints.Add(hint);
        OnPropertyChanged(nameof(HasVehicleHints));
    }

    private void SelectMobilityValue(MobilityType value)
    {
        foreach (var o in Mobilities) o.IsSelected = o.Value == value;
        SelectedMobility = Mobilities.FirstOrDefault(o => o.Value == value);
    }

    // ───────────────────────────── 1. Vehicle: commands ─────────────────────────────

    [RelayCommand]
    private void SelectMobility(MobilityOption option)
    {
        if (!IsEditable) return;
        SelectMobilityValue(option.Value);
        Message = string.Empty;
    }

    [RelayCommand]
    private async Task TakeFront()
    {
        if (!IsEditable) return;
        if (await TakeLivePhotoAsync() is { } photo) { _front = photo; FrontPhoto = photo.FullPath; }
    }

    [RelayCommand]
    private async Task TakeSide()
    {
        if (!IsEditable) return;
        if (await TakeLivePhotoAsync() is { } photo) { _side = photo; SidePhoto = photo.FullPath; }
    }

    /// <summary>Sends the vehicle details; the AI checks the photos straight away and the hints show at once.</summary>
    [RelayCommand]
    private async Task SendVehicle()
    {
        if (IsBusy) return;
        if (SelectedMobility is null) { Message = "Please choose how you get around."; return; }
        if (NeedsRegistration && string.IsNullOrWhiteSpace(RegistrationNumber))
        {
            Message = "Please type the registration number, as on the number plate.";
            return;
        }
        if (NeedsPhotos && (_front is null || _side is null))
        {
            Message = "Please take both photos with your camera: the front (number plate showing) and the side.";
            return;
        }

        IsBusy = true;
        Message = string.Empty;
        try
        {
            var result = await _couriers.SendVehicleAsync(SelectedMobility.Value, RegistrationNumber, Make, Colour,
                                                          NeedsPhotos ? _front : null, NeedsPhotos ? _side : null);
            if (result.IsFailure) { Message = result.Error!.DisplayMessage; return; }
            ShowVehicle(result.Value!);
            await ReloadAsync();   // the documents needed depend on the vehicle
        }
        catch (Exception ex)
        {
            Message = $"Couldn't send the photos: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ───────────────────────────── 2. Documents ─────────────────────────────

    /// <summary>Takes the document's photo, then sends it with its number straight away.</summary>
    [RelayCommand]
    private async Task TakeDocument(CourierDocumentRow row)
    {
        if (IsBusy || !IsEditable) return;
        if (row.NeedsNumber && string.IsNullOrWhiteSpace(row.Number))
        {
            Message = $"Please type the number of your {row.Name} first. {row.NumberHelp}";
            return;
        }

        var photo = await TakeLivePhotoAsync();
        if (photo is null) return;

        IsBusy = true;
        row.IsSending = true;
        Message = string.Empty;
        try
        {
            var result = await _couriers.SendDocumentAsync(row.Type, row.Number, photo);
            if (result.IsFailure) { Message = result.Error!.DisplayMessage; return; }
            Show(result.Value!);
        }
        catch (Exception ex)
        {
            Message = $"Couldn't send the photo: {ex.Message}";
        }
        finally
        {
            row.IsSending = false;
            IsBusy = false;
        }
    }

    // ───────────────────────────── 3. Send ─────────────────────────────

    [RelayCommand]
    private async Task Submit()
    {
        if (IsBusy) return;
        IsBusy = true;
        Message = string.Empty;
        try
        {
            var result = await _couriers.SubmitAsync();
            if (result.IsFailure) { Message = result.Error!.DisplayMessage; return; }
            Show(result.Value!);
            await Shell.Current.DisplayAlertAsync("Sent ✓",
                "Thank you. Phloem staff will check your details and email you.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task EditProfile() => await Shell.Current.GoToAsync("editprofile");

    private async Task ReloadAsync()
    {
        var result = await _couriers.GetMineAsync();
        if (result.IsSuccess) Show(result.Value!);
    }

    /// <summary>A photo from the camera only (no gallery), so it's taken now, of the real thing.</summary>
    private async Task<FileResult?> TakeLivePhotoAsync()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                Message = "This needs a phone with a camera: the photos must be taken now, not chosen from the gallery.";
                return null;
            }
            return await MediaPicker.Default.CapturePhotoAsync();
        }
        catch (PermissionException)
        {
            await Shell.Current.DisplayAlertAsync("Camera permission",
                "Please allow Phloem to use the camera in your phone settings.", "OK");
            return null;
        }
        catch (Exception ex)
        {
            Message = $"Couldn't take the photo: {ex.Message}";
            return null;
        }
    }
}
