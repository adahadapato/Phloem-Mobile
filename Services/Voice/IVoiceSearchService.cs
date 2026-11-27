namespace Phloem.Services.Voice;

/// <summary>
/// The phone's own speech recognition, for the 🎤 on the search bars. One implementation per platform:
/// <c>Platforms/Android/Voice/AndroidVoiceSearchService</c> (Android SpeechRecognizer),
/// <c>Platforms/iOS/Voice/IosVoiceSearchService</c> (Apple's Speech framework), and
/// <see cref="NoVoiceSearchService"/> elsewhere (the 🎤 stays hidden).
/// </summary>
/// <remarks>
/// <para>Phloem only receives the TEXT. Depending on the phone, Google or Apple may process the audio on their
/// servers (the privacy policy needs a line for this).</para>
/// <para>Dish names are often misheard ("kelly wally"); the API's "sounds-like" search finds the dish anyway. iPhones
/// also get the dish names as hints, so they hear them better.</para>
/// <para>The microphone permission is asked by <see cref="VoiceSearch"/> before calling <see cref="ListenAsync"/>.</para>
/// </remarks>
public interface IVoiceSearchService
{
    /// <summary>True when the phone has speech recognition (checked each time: it can be switched off).</summary>
    bool IsSupported { get; }

    /// <summary>
    /// Listens until the person stops talking (or <see cref="Stop"/>, or about 10 seconds).
    /// </summary>
    /// <param name="locale">"en-GB", "en-GH" or "en-NG" (from the country flag); the phone falls back to English.</param>
    /// <param name="hints">Words it should expect (dish names); used where the phone supports it (iPhone).</param>
    /// <param name="onWords">Called with the words so far, on the main thread, so the search box fills as they speak.</param>
    /// <param name="ct">Cancels listening (nothing is returned).</param>
    /// <returns>What was heard, or why nothing was.</returns>
    Task<VoiceSearchResult> ListenAsync(string locale, IReadOnlyCollection<string> hints, Action<string> onWords,
                                        CancellationToken ct);

    /// <summary>Stops listening now and keeps what was heard so far (the 🎤 tapped again).</summary>
    void Stop();
}
