using Android.Content;
using Android.OS;
using Android.Speech;

namespace Phloem.Services.Voice;

/// <summary>
/// Voice search on Android: the phone's <see cref="SpeechRecognizer"/> (Google's speech service on most phones).
/// No pop-up of its own: the 🎤's glowing ring shows it's listening, and the words appear in the search box as
/// they're said.
/// </summary>
/// <remarks>
/// <para>Needs <c>RECORD_AUDIO</c> (asked by <see cref="VoiceSearch"/>) and, on Android 11+, the
/// <c>android.speech.RecognitionService</c> entry in the manifest's &lt;queries&gt; so the app can see the service.</para>
/// <para>Android stops by itself when the person stops talking. Everything runs on the main thread (Android's rule).</para>
/// </remarks>
public sealed class AndroidVoiceSearchService : IVoiceSearchService
{
    private SpeechRecognizer? _recognizer;

    /// <inheritdoc />
    public bool IsSupported => SpeechRecognizer.IsRecognitionAvailable(Microsoft.Maui.ApplicationModel.Platform.AppContext);

    /// <inheritdoc />
    public async Task<VoiceSearchResult> ListenAsync(string locale, IReadOnlyCollection<string> hints,
                                                     Action<string> onWords, CancellationToken ct)
    {
        var listener = new RecognitionListener(onWords);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            _recognizer?.Destroy();
            _recognizer = SpeechRecognizer.CreateSpeechRecognizer(Microsoft.Maui.ApplicationModel.Platform.AppContext)
                          ?? throw new InvalidOperationException("No speech recognizer on this phone.");
            _recognizer.SetRecognitionListener(listener);

            var intent = new Intent(RecognizerIntent.ActionRecognizeSpeech);
            intent.PutExtra(RecognizerIntent.ExtraLanguageModel, RecognizerIntent.LanguageModelFreeForm);
            intent.PutExtra(RecognizerIntent.ExtraLanguage, locale);
            intent.PutExtra(RecognizerIntent.ExtraPartialResults, true);
            intent.PutExtra(RecognizerIntent.ExtraMaxResults, 1);
            _recognizer.StartListening(intent);
        });

        await using var cancel = ct.Register(() => MainThread.BeginInvokeOnMainThread(() =>
        {
            _recognizer?.Cancel();
            listener.Finish(VoiceSearchResult.Nothing);
        }));

        try
        {
            return await listener.Result;
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _recognizer?.Destroy();
                _recognizer = null;
            });
        }
    }

    /// <inheritdoc />
    public void Stop() => MainThread.BeginInvokeOnMainThread(() => _recognizer?.StopListening());

    /// <summary>Receives Android's answers and turns them into one result.</summary>
    private sealed class RecognitionListener(Action<string> onWords) : Java.Lang.Object, IRecognitionListener
    {
        private readonly TaskCompletionSource<VoiceSearchResult> _result =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private string? _lastWords;

        /// <summary>Completes when listening ends.</summary>
        public Task<VoiceSearchResult> Result => _result.Task;

        /// <summary>Ends with this result (only the first call counts).</summary>
        public void Finish(VoiceSearchResult result) => _result.TrySetResult(result);

        public void OnPartialResults(Bundle? partialResults)
        {
            var words = Best(partialResults);
            if (string.IsNullOrWhiteSpace(words)) return;
            _lastWords = words;
            onWords(words);
        }

        public void OnResults(Bundle? results)
        {
            var words = Best(results) ?? _lastWords;
            Finish(string.IsNullOrWhiteSpace(words) ? VoiceSearchResult.Nothing : new VoiceSearchResult(words.Trim()));
        }

        public void OnError(SpeechRecognizerError error) => Finish(error switch
        {
            // Silence / nothing recognised: just stop quietly (keep anything heard so far)
            SpeechRecognizerError.NoMatch or SpeechRecognizerError.SpeechTimeout or SpeechRecognizerError.Client
                => _lastWords is null ? VoiceSearchResult.Nothing : new VoiceSearchResult(_lastWords),
            SpeechRecognizerError.InsufficientPermissions
                => new VoiceSearchResult(null, "Phloem needs the microphone to search by voice."),
            SpeechRecognizerError.Network or SpeechRecognizerError.NetworkTimeout or SpeechRecognizerError.Server
                => new VoiceSearchResult(null, "Voice search needs an internet connection. Please type instead."),
            _ => new VoiceSearchResult(null, "Voice search isn't working right now. Please type instead.")
        });

        public void OnBeginningOfSpeech() { }
        public void OnBufferReceived(byte[]? buffer) { }
        public void OnEndOfSpeech() { }
        public void OnEvent(int eventType, Bundle? @params) { }
        public void OnReadyForSpeech(Bundle? @params) { }
        public void OnRmsChanged(float rmsdB) { }

        private static string? Best(Bundle? bundle) =>
            bundle?.GetStringArrayList(SpeechRecognizer.ResultsRecognition)?.FirstOrDefault();
    }
}
