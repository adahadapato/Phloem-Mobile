using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Voice;

/// <summary>
/// The 🎤 on the Home and Explore search bars: asks for the microphone, picks the accent from the country flag, and
/// listens (<see cref="IVoiceSearchService"/>). Shared, so both search bars behave the same.
/// </summary>
/// <remarks>
/// While listening, the words appear in the search box as they're said; when the person stops talking the normal
/// search runs (and the API's "sounds-like" matching finds dishes whose names were misheard).
/// </remarks>
public sealed class VoiceSearch(IVoiceSearchService speech)
{
    private CancellationTokenSource? _listening;

    /// <summary>True when the 🎤 should show.</summary>
    public bool IsSupported => speech.IsSupported;

    /// <summary>True while listening.</summary>
    public bool IsListening => _listening is not null;

    /// <summary>
    /// Listens once. Tapping the 🎤 again while listening calls <see cref="StopListening"/> instead.
    /// </summary>
    /// <param name="country">The country on the flag (accent); null = UK English.</param>
    /// <param name="hints">Dish names on screen, to help the phone hear them.</param>
    /// <param name="onWords">The words so far (fill the search box).</param>
    /// <returns>What was heard, or a message to show.</returns>
    public async Task<VoiceSearchResult> ListenAsync(Country? country, IReadOnlyCollection<string> hints,
                                                     Action<string> onWords)
    {
        if (_listening is not null) return VoiceSearchResult.Nothing;

        var listening = _listening = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        try
        {
            // Permissions must be asked on the main thread (Android throws otherwise)
            var permission = await MainThread.InvokeOnMainThreadAsync(async () =>
            {
                var status = await Permissions.CheckStatusAsync<Permissions.Microphone>();
                return status == PermissionStatus.Granted ? status : await Permissions.RequestAsync<Permissions.Microphone>();
            });
            if (permission != PermissionStatus.Granted)
                return new VoiceSearchResult(null,
                    "Phloem needs the microphone to search by voice. You can allow it in your phone's settings.");

            return await speech.ListenAsync(LocaleFor(country), hints, onWords, listening.Token);
        }
        catch (OperationCanceledException)
        {
            return VoiceSearchResult.Nothing;
        }
        catch (Exception ex)
        {
            return new VoiceSearchResult(null, Problem(ex));
        }
        finally
        {
            _listening = null;
            listening.Dispose();
        }
    }

    /// <summary>Stops listening and keeps what was heard.</summary>
    public void StopListening() => speech.Stop();

    /// <summary>The message for an unexpected error; Debug builds add the cause, to help fix it.</summary>
    public static string Problem(Exception ex)
    {
        System.Diagnostics.Debug.WriteLine($"[VoiceSearch] {ex}");
#if DEBUG
        return $"Voice search isn't working right now. Please type instead.\n\n({ex.GetType().Name}: {ex.Message})";
#else
        return "Voice search isn't working right now. Please type instead.";
#endif
    }

    /// <summary>The accent from the country flag.</summary>
    private static string LocaleFor(Country? country) => country switch
    {
        Country.Ghana => "en-GH",
        Country.Nigeria => "en-NG",
        _ => "en-GB"
    };
}
