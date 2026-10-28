using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Services;

namespace Phloem.ViewModels;

/// <summary>One "How did you hand it over?" choice.</summary>
public sealed partial class HandoverOption : ObservableObject
{
    public required HandoverMethod Value { get; init; }
    public required string Label { get; init; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// "Delivered" (route "deliveryproof", parameters Kind, Reference, Recipient): the courier takes a photo of where they
/// left it (always required), says how it was handed over (and to whom, for a neighbour), adds an optional note,
/// then Confirm. The API saves the photo privately, marks it delivered and emails the customer.
/// </summary>
/// <remarks>The camera opens by itself the first time the screen appears. Choosing from the gallery is only offered
/// on devices without a camera.</remarks>
public partial class DeliveryProofViewModel : ObservableObject, IQueryAttributable
{
    /// <summary>The API's limits.</summary>
    public const int ReceivedByMaxLength = 100;
    public const int NoteMaxLength = 300;

    private readonly DeliveryService _deliveries;
    private FileResult? _photo;
    private bool _cameraOffered;
    private bool _saving;

    public DeliveryProofViewModel(DeliveryService deliveries)
    {
        _deliveries = deliveries;
        Handovers =
        [
            new HandoverOption { Value = HandoverMethod.InPerson, Label = "🤝  Handed to them" },
            new HandoverOption { Value = HandoverMethod.FrontDoor, Label = "🚪  Front door" },
            new HandoverOption { Value = HandoverMethod.Neighbour, Label = "🏠  Neighbour" },
            new HandoverOption { Value = HandoverMethod.SafePlace, Label = "📦  Safe place" },
            new HandoverOption { Value = HandoverMethod.Reception, Label = "🛎  Reception / gate" }
        ];
    }

    public ObservableCollection<HandoverOption> Handovers { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TitleText))]
    public partial DeliveryKind Kind { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TitleText))]
    public partial string Reference { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ForText))]
    public partial string Recipient { get; set; } = string.Empty;

    public string TitleText => (Kind == DeliveryKind.Parcel ? "Parcel " : "Order ") + Reference;
    public string ForText => string.IsNullOrWhiteSpace(Recipient) ? string.Empty : $"For {Recipient}";

    /// <summary>The photo on screen (a local file); null until one is taken.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPhoto), nameof(HasNoPhoto), nameof(PhotoButtonText))]
    public partial string? PhotoPath { get; set; }

    public bool HasPhoto => !string.IsNullOrEmpty(PhotoPath);
    public bool HasNoPhoto => !HasPhoto;
    public string PhotoButtonText => HasPhoto ? "📷  Retake photo" : "📷  Take a photo";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowReceivedBy), nameof(ReceivedByLabel), nameof(ReceivedByPlaceholder))]
    public partial HandoverOption? SelectedHandover { get; set; }

    /// <summary>"Who took it?" for in person, neighbour and reception.</summary>
    public bool ShowReceivedBy => SelectedHandover?.Value is HandoverMethod.InPerson or HandoverMethod.Neighbour
                                                          or HandoverMethod.Reception;

    public string ReceivedByLabel => SelectedHandover?.Value == HandoverMethod.Neighbour
        ? "Which neighbour?"
        : "Who took it? (optional)";

    public string ReceivedByPlaceholder => SelectedHandover?.Value switch
    {
        HandoverMethod.Neighbour => "e.g. Mrs Ade at No. 14",
        HandoverMethod.Reception => "e.g. Musa at the gate",
        _ => "e.g. Ama"
    };

    [ObservableProperty]
    public partial string ReceivedBy { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Note { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    public partial string Message { get; set; } = string.Empty;

    public bool HasMessage => Message.Length > 0;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Kind", out var kind) && kind is DeliveryKind k) Kind = k;
        if (query.TryGetValue("Reference", out var reference)) Reference = reference?.ToString() ?? string.Empty;
        if (query.TryGetValue("Recipient", out var recipient)) Recipient = recipient?.ToString() ?? string.Empty;

        // A fresh delivery: nothing from the last one
        _photo = null;
        PhotoPath = null;
        SelectedHandover = null;
        foreach (var option in Handovers) option.IsSelected = false;
        ReceivedBy = string.Empty;
        Note = string.Empty;
        Message = string.Empty;
        _cameraOffered = false;
    }

    /// <summary>The first time the screen appears: straight to the camera.</summary>
    public async Task OnAppearingAsync()
    {
        if (_cameraOffered || HasPhoto) return;
        _cameraOffered = true;
        await TakePhoto();
    }

    [RelayCommand]
    private async Task TakePhoto()
    {
        try
        {
            if (!MediaPicker.Default.IsCaptureSupported)
            {
                var pick = await Shell.Current.DisplayAlertAsync("No camera",
                    "This device can't take photos. Choose a photo you've already taken?", "Choose photo", "Cancel");
                if (pick) Use((await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = 1 }))
                              ?.FirstOrDefault());
                return;
            }

            Use(await MediaPicker.Default.CapturePhotoAsync());
        }
        catch (PermissionException)
        {
            await Shell.Current.DisplayAlertAsync("Camera permission",
                "Please allow Phloem to use the camera in your phone settings: a photo is needed to confirm delivery.",
                "OK");
        }
        catch (Exception ex)
        {
            await Shell.Current.DisplayAlertAsync("Couldn't take the photo", ex.Message, "OK");
        }
    }

    private void Use(FileResult? photo)
    {
        if (photo is null) return;
        _photo = photo;
        PhotoPath = photo.FullPath;
        if (Message.Length > 0) Message = string.Empty;
    }

    [RelayCommand]
    private void SelectHandover(HandoverOption option)
    {
        foreach (var o in Handovers) o.IsSelected = ReferenceEquals(o, option);
        SelectedHandover = option;
        if (Message.Length > 0) Message = string.Empty;
    }

    /// <summary>Checks, then sends the photo and details; back to the list when it worked.</summary>
    [RelayCommand]
    private async Task Confirm()
    {
        if (_saving) return;

        if (_photo is null)
        {
            Message = "Please take a photo of where you left it.";
            return;
        }
        if (SelectedHandover is null)
        {
            Message = "Please choose how you handed it over.";
            return;
        }
        if (SelectedHandover.Value == HandoverMethod.Neighbour && string.IsNullOrWhiteSpace(ReceivedBy))
        {
            Message = "Please say which neighbour took it.";
            return;
        }

        _saving = true;
        IsBusy = true;
        Message = string.Empty;
        try
        {
            await using var stream = await _photo.OpenReadAsync();
            var contentType = string.IsNullOrWhiteSpace(_photo.ContentType) ? "image/jpeg" : _photo.ContentType;
            var who = ShowReceivedBy ? ReceivedBy : null;

            var result = await _deliveries.MarkDeliveredAsync(Kind, Reference, stream, _photo.FileName, contentType,
                                                              SelectedHandover.Value, who, Note);
            if (result.IsFailure)
            {
                Message = result.Error!.DisplayMessage;
                return;
            }

            await Shell.Current.DisplayAlertAsync("Delivered ✓",
                Kind == DeliveryKind.Parcel
                    ? "Thanks! The sender has been told, with your photo."
                    : "Thanks! The customer has been told, with your photo.",
                "OK");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex)
        {
            Message = $"Couldn't send the photo: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            _saving = false;
        }
    }
}
