using Plugin.Maui.Audio;

namespace Phloem.Services.Pronunciation;

/// <summary>
/// Staff record a dish name said properly (Admin → Dish names → Pronunciation). Uses <c>Plugin.Maui.Audio</c>,
/// which records a wav file on Android and iPhone (the API accepts m4a, mp3 and wav up to 2 MB, about 10 seconds
/// of wav).
/// </summary>
public sealed class PronunciationRecorder
{
    private IAudioRecorder? _recorder;

    /// <summary>True while recording.</summary>
    public bool IsRecording => _recorder?.IsRecording == true;

    /// <summary>Starts recording (asks for the microphone first).</summary>
    /// <returns>Null when recording; otherwise why not.</returns>
    public async Task<string?> StartAsync()
    {
        var permission = await Permissions.CheckStatusAsync<Permissions.Microphone>();
        if (permission != PermissionStatus.Granted)
            permission = await Permissions.RequestAsync<Permissions.Microphone>();
        if (permission != PermissionStatus.Granted)
            return "Phloem needs the microphone to record. You can allow it in your phone's settings.";

        try
        {
            _recorder = AudioManager.Current.CreateRecorder();
            if (!_recorder.CanRecordAudio) return "This phone can't record audio.";
            await _recorder.StartAsync();
            return null;
        }
        catch (Exception)
        {
            _recorder = null;
            return "The recording couldn't start. Please try again.";
        }
    }

    /// <summary>Stops recording.</summary>
    /// <returns>The recording as a wav file, or null when nothing was recorded.</returns>
    public async Task<byte[]?> StopAsync()
    {
        var recorder = _recorder;
        _recorder = null;
        if (recorder is null || !recorder.IsRecording) return null;

        try
        {
            var source = await recorder.StopAsync();
            await using var stream = source.GetAudioStream();
            using var copy = new MemoryStream();
            await stream.CopyToAsync(copy);
            return copy.Length == 0 ? null : copy.ToArray();
        }
        catch (Exception)
        {
            return null;
        }
    }
}
