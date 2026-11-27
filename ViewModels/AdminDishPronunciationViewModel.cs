using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Services;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Pronunciation;

namespace Phloem.ViewModels;

/// <summary>
/// Admin → Dish names → a name → "Pronunciation" (route "admindishsound"): how a dish name is said.
/// </summary>
/// <remarks>
/// <list type="bullet">
///   <item><description><b>Written guide</b> ("WAH-chay"): shown under the name for sellers and buyers.</description></item>
///   <item><description><b>Other spellings</b>: misspellings and what phones mishear ("whatchy"), one per line, max
///   10; search uses them so typed and voice search still find the dish.</description></item>
///   <item><description><b>Recording</b>: record (up to 10 seconds), listen back, then upload; or remove the current
///   one.</description></item>
/// </list>
/// Opened with the name: <c>GoToAsync("admindishsound", { ["DishName"] = AdminDishCatalogDto })</c>.
/// </remarks>
public partial class AdminDishPronunciationViewModel(AdminService admin, PronunciationPlayer player,
                                                     PronunciationRecorder recorder)
    : ObservableObject, IQueryAttributable
{
    private const int MaxSeconds = 10;
    private AdminDishCatalogDto? _item;
    private byte[]? _newRecording;
    private CancellationTokenSource? _autoStop;

    /// <summary>The dish name.</summary>
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    /// <summary>"WAH-chay".</summary>
    [ObservableProperty]
    public partial string Guide { get; set; } = string.Empty;

    /// <summary>Other spellings, one per line.</summary>
    [ObservableProperty]
    public partial string Aliases { get; set; } = string.Empty;

    /// <summary>The saved recording's link (null = none).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSavedRecording), nameof(RecordingText))]
    public partial string? SavedAudioUrl { get; set; }

    /// <summary>True while recording.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordButtonText))]
    public partial bool IsRecording { get; set; }

    /// <summary>True when a new recording is waiting to be uploaded.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordingText))]
    public partial bool HasNewRecording { get; set; }

    /// <summary>True while saving.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>The message at the top.</summary>
    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    /// <summary>True when <see cref="Message"/> is an error.</summary>
    [ObservableProperty]
    public partial bool IsError { get; set; }

    public bool HasSavedRecording => !string.IsNullOrEmpty(SavedAudioUrl);

    public string RecordButtonText => IsRecording ? "■  Stop" : "●  Record";

    public string RecordingText => HasNewRecording
        ? "New recording ready: listen, then Upload."
        : HasSavedRecording ? "A recording is saved." : "No recording yet.";

    /// <inheritdoc />
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (!query.TryGetValue("DishName", out var value) || value is not AdminDishCatalogDto item) return;
        _item = item;
        Name = item.Name;
        Guide = item.PronunciationGuide ?? string.Empty;
        Aliases = string.Join(Environment.NewLine, item.Aliases ?? []);
        SavedAudioUrl = item.PronunciationAudioUrl;
    }

    /// <summary>Saves the guide and the other spellings.</summary>
    [RelayCommand]
    private async Task SaveText()
    {
        if (_item is null || IsBusy) return;
        var aliases = Aliases.Split(['\n', '\r', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                             .ToList();

        IsBusy = true;
        try
        {
            var saved = await admin.SetPronunciationAsync(_item.Id, Guide, aliases);
            Show(saved.IsSuccess ? "Saved. Search now uses these spellings." : saved.Error!.DisplayMessage, !saved.IsSuccess);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>● Record / ■ Stop (stops by itself after 10 seconds).</summary>
    [RelayCommand]
    private async Task Record()
    {
        if (IsRecording)
        {
            await StopRecordingAsync();
            return;
        }

        player.Stop();
        var problem = await recorder.StartAsync();
        if (problem is not null)
        {
            Show(problem, true);
            return;
        }

        IsRecording = true;
        Show("Recording… say the name clearly, once.", false);
        var autoStop = _autoStop = new CancellationTokenSource();
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(MaxSeconds), autoStop.Token);
            await StopRecordingAsync();
        }
        catch (OperationCanceledException)
        {
            // Stopped by hand
        }
    }

    private async Task StopRecordingAsync()
    {
        _autoStop?.Cancel();
        _autoStop = null;
        var recording = await recorder.StopAsync();
        IsRecording = false;

        _newRecording = recording;
        HasNewRecording = recording is not null;
        Show(recording is null ? "Nothing was recorded. Please try again." : "Recorded. Listen, then Upload.",
             recording is null);
    }

    /// <summary>▶ Plays the new recording, or the saved one.</summary>
    [RelayCommand]
    private async Task Play()
    {
        if (_newRecording is not null)
        {
            player.Play(_newRecording);
            return;
        }
        if (!await player.PlayAsync(SavedAudioUrl)) Show("The recording couldn't be played.", true);
    }

    /// <summary>Uploads the new recording (replaces the saved one).</summary>
    [RelayCommand]
    private async Task Upload()
    {
        if (_item is null || _newRecording is null || IsBusy) return;

        IsBusy = true;
        try
        {
            using var audio = new MemoryStream(_newRecording);
            var uploaded = await admin.UploadPronunciationAsync(_item.Id, audio, $"{_item.Name}.wav", "audio/wav");
            if (uploaded.IsFailure)
            {
                Show(uploaded.Error!.DisplayMessage, true);
                return;
            }

            SavedAudioUrl = uploaded.Value;
            _newRecording = null;
            HasNewRecording = false;
            Show("Uploaded. Sellers and buyers can now hear it with 🔊.", false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Removes the saved recording (after a confirmation).</summary>
    [RelayCommand]
    private async Task Remove()
    {
        if (_item is null || !HasSavedRecording || IsBusy) return;
        if (!await Shell.Current.DisplayAlertAsync("Remove recording?", $"Remove how \"{Name}\" is said?", "Remove", "Keep"))
            return;

        IsBusy = true;
        try
        {
            var removed = await admin.RemovePronunciationAsync(_item.Id);
            if (removed.IsFailure)
            {
                Show(removed.Error!.DisplayMessage, true);
                return;
            }
            SavedAudioUrl = null;
            Show("Recording removed.", false);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Leaving the page: stop recording and playing.</summary>
    public async Task LeaveAsync()
    {
        player.Stop();
        if (IsRecording) await StopRecordingAsync();
    }

    private void Show(string message, bool isError)
    {
        Message = message;
        IsError = isError;
    }
}
