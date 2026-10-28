using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Models;
using Phloem.Services;

namespace Phloem.ViewModels;

/// <summary>
/// "My deliveries" (route "deliveries"): the food orders and parcels given to the signed-in courier (or a seller
/// delivering their own orders). Two tabs: <b>To deliver</b> (oldest first) and <b>Delivered</b> (last 7 days).
/// Each card: where to collect it, who to call, the address (opens the map) and <b>Delivered</b>, which opens the
/// camera for the proof photo.
/// </summary>
public partial class MyDeliveriesViewModel : ObservableObject
{
    private readonly DeliveryService _deliveries;
    private CancellationTokenSource? _loading;

    public MyDeliveriesViewModel(DeliveryService deliveries)
    {
        _deliveries = deliveries;
    }

    public ObservableCollection<DeliveryCard> Cards { get; } = new();

    /// <summary>False = "To deliver", true = "Delivered".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsToDoTab), nameof(EmptyText))]
    public partial bool ShowDone { get; set; }

    public bool IsToDoTab => !ShowDone;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage), nameof(ShowEmpty))]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    public partial bool IsError { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmpty))]
    public partial bool IsEmpty { get; set; }

    public bool HasMessage => Message.Length > 0;

    /// <summary>"Nothing to deliver" (not while an error or a "Delivered" message is showing).</summary>
    public bool ShowEmpty => IsEmpty && !IsError;

    public string EmptyText => ShowDone
        ? "Nothing delivered in the last 7 days."
        : "Nothing to deliver right now. New deliveries appear here, and we email you too.";

    /// <summary>Called each time the page appears (and on pull to refresh).</summary>
    public async Task LoadAsync()
    {
        _loading?.Cancel();
        var cts = _loading = new CancellationTokenSource();

        IsLoading = true;
        try
        {
            var result = await _deliveries.GetMineAsync(ShowDone, cts.Token);
            if (cts.IsCancellationRequested) return;

            if (result.IsFailure)
            {
                IsError = true;
                Message = result.Error!.DisplayMessage;
                return;
            }

            Cards.Clear();
            foreach (var job in result.Value!) Cards.Add(new DeliveryCard { Job = job });
            IsEmpty = Cards.Count == 0;
            if (IsError) Message = string.Empty;
            IsError = false;
        }
        catch (OperationCanceledException)
        {
            // another tab was picked: its own load shows the list
        }
        finally
        {
            if (ReferenceEquals(_loading, cts)) IsLoading = false;
        }
    }

    /// <summary>Shown after coming back from "Delivered".</summary>
    public void ShowDoneMessage(string text)
    {
        IsError = false;
        Message = text;
    }

    [RelayCommand]
    private async Task Refresh() => await LoadAsync();

    [RelayCommand]
    private async Task ShowToDeliver()
    {
        if (!ShowDone) return;
        ShowDone = false;
        Message = string.Empty;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ShowDelivered()
    {
        if (ShowDone) return;
        ShowDone = true;
        Message = string.Empty;
        await LoadAsync();
    }

    [RelayCommand]
    private async Task CallRecipient(DeliveryCard card) => await CallAsync(card.Job.RecipientPhone);

    [RelayCommand]
    private async Task CallShop(DeliveryCard card) => await CallAsync(card.Job.CollectPhone);

    /// <summary>Opens the address in the phone's maps app, ready for directions.</summary>
    [RelayCommand]
    private async Task OpenMap(DeliveryCard card)
    {
        try
        {
            var place = new Placemark { Thoroughfare = card.Job.Address, CountryName = CountryOption.NameOf(card.Job.Country) };
            await Map.Default.OpenAsync(place, new MapLaunchOptions { NavigationMode = NavigationMode.Driving });
        }
        catch (Exception)
        {
            await Shell.Current.DisplayAlertAsync("No maps app", card.Job.Address, "OK");
        }
    }

    /// <summary>Delivered → the photo screen.</summary>
    [RelayCommand]
    private async Task Delivered(DeliveryCard card) =>
        await Shell.Current.GoToAsync("deliveryproof", new Dictionary<string, object>
        {
            ["Kind"] = card.Kind,
            ["Reference"] = card.Reference,
            ["Recipient"] = card.Job.RecipientName
        });

    private static async Task CallAsync(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return;
        try { await Launcher.Default.OpenAsync(new Uri($"tel:{phone}")); }
        catch (Exception) { await Shell.Current.DisplayAlertAsync("Can't call from here", phone, "OK"); }
    }
}
