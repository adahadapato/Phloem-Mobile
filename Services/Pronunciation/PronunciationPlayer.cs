using Plugin.Maui.Audio;

namespace Phloem.Services.Pronunciation;

/// <summary>
/// The 🔊 next to a dish name: plays the recording of the name said properly (from the API's public storage).
/// </summary>
/// <remarks>
/// Clips are a few KB, so each one is downloaded once and kept in memory for the rest of the session. Uses
/// <c>Plugin.Maui.Audio</c>. Playing a new clip stops the one before. Never throws: a clip that can't play is skipped.
/// </remarks>
public sealed class PronunciationPlayer
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly Dictionary<string, byte[]> _clips = new(StringComparer.Ordinal);
    private IAudioPlayer? _playing;

    /// <summary>Plays the clip at this link.</summary>
    /// <param name="url">The recording's public link (<c>pronunciationAudioUrl</c>).</param>
    /// <returns>True when it started playing.</returns>
    public async Task<bool> PlayAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;

        try
        {
            if (!_clips.TryGetValue(url, out var bytes))
            {
                bytes = await Http.GetByteArrayAsync(url);
                _clips[url] = bytes;
            }

            return await MainThread.InvokeOnMainThreadAsync(() => Play(bytes));
        }
        catch (Exception)
        {
            return false;   // offline, or a file the phone can't play
        }
    }

    /// <summary>Plays a clip that is already in memory (e.g. one just recorded by staff).</summary>
    /// <param name="bytes">The audio file.</param>
    public bool Play(byte[] bytes)
    {
        Stop();
        var player = AudioManager.Current.CreatePlayer(new MemoryStream(bytes));
        player.PlaybackEnded += (_, _) =>
        {
            if (ReferenceEquals(_playing, player)) _playing = null;
            player.Dispose();
        };
        _playing = player;
        player.Play();
        return true;
    }

    /// <summary>Stops whatever is playing.</summary>
    public void Stop()
    {
        var playing = _playing;
        _playing = null;
        if (playing is null) return;
        playing.Stop();
        playing.Dispose();
    }
}
