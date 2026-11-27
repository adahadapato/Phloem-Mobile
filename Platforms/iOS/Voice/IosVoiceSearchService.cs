using AVFoundation;
using Foundation;
using Speech;

namespace Phloem.Services.Voice;

/// <summary>
/// Voice search on iPhone: Apple's Speech framework (<see cref="SFSpeechRecognizer"/>) fed from the microphone.
/// </summary>
/// <remarks>
/// <para>Dish names on screen are passed as <c>ContextualStrings</c>, so "waakye" or "kelewele" are heard better.</para>
/// <para>iOS doesn't stop by itself when the person stops talking, so listening ends after 1.5 seconds without new
/// words (or 10 seconds in all, or the 🎤 tapped again).</para>
/// <para>Asks for speech-recognition permission the first time (the microphone permission is asked by
/// <see cref="VoiceSearch"/>). Info.plist needs <c>NSMicrophoneUsageDescription</c> and
/// <c>NSSpeechRecognitionUsageDescription</c>.</para>
/// </remarks>
public sealed class IosVoiceSearchService : IVoiceSearchService
{
    private static readonly TimeSpan Silence = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan Longest = TimeSpan.FromSeconds(10);

    private AVAudioEngine? _engine;
    private SFSpeechAudioBufferRecognitionRequest? _request;
    private SFSpeechRecognitionTask? _task;

    /// <inheritdoc />
    public bool IsSupported
    {
        get
        {
            using var recognizer = new SFSpeechRecognizer();
            return recognizer.Available;
        }
    }

    /// <inheritdoc />
    public async Task<VoiceSearchResult> ListenAsync(string locale, IReadOnlyCollection<string> hints,
                                                     Action<string> onWords, CancellationToken ct)
    {
        if (await RequestSpeechPermissionAsync() != SFSpeechRecognizerAuthorizationStatus.Authorized)
            return new VoiceSearchResult(null,
                "Phloem needs speech recognition to search by voice. You can allow it in Settings → Phloem.");

        var recognizer = new SFSpeechRecognizer(new NSLocale(locale.Replace('-', '_')));
        if (!recognizer.Available)
            recognizer = new SFSpeechRecognizer(new NSLocale("en_GB"));   // e.g. en-GH not offered on this phone
        if (!recognizer.Available)
            return new VoiceSearchResult(null, "Voice search isn't available right now. Please type instead.");

        var done = new TaskCompletionSource<VoiceSearchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        string? lastWords = null;

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            var session = AVAudioSession.SharedInstance();
            session.SetCategory(AVAudioSessionCategory.Record, AVAudioSessionCategoryOptions.DuckOthers, out _);
            session.SetMode(AVAudioSession.ModeMeasurement, out _);
            session.SetActive(true, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation, out _);

            _engine = new AVAudioEngine();
            _request = new SFSpeechAudioBufferRecognitionRequest
            {
                ShouldReportPartialResults = true,
                ContextualStrings = hints.Take(100).ToArray()
            };

            var input = _engine.InputNode;
            var format = input.GetBusOutputFormat(0);
            if (format.SampleRate <= 0 || format.ChannelCount == 0)
            {
                done.TrySetResult(new VoiceSearchResult(null, "The microphone isn't available right now. Please type instead."));
                return;
            }
            var request = _request;
            input.InstallTapOnBus(0, 1024, format, (buffer, _) => request.Append(buffer));
            _engine.Prepare();
            if (!_engine.StartAndReturnError(out _))
            {
                done.TrySetResult(new VoiceSearchResult(null, "The microphone couldn't start. Please type instead."));
                return;
            }

            _task = recognizer.GetRecognitionTask(_request, (result, error) =>
            {
                if (result is not null)
                {
                    var words = result.BestTranscription.FormattedString;
                    if (!string.IsNullOrWhiteSpace(words))
                    {
                        lastWords = words;
                        MainThread.BeginInvokeOnMainThread(() => onWords(words));
                    }
                    if (result.Final)
                    {
                        done.TrySetResult(new VoiceSearchResult(lastWords?.Trim()));
                        return;
                    }

                    // Restart the "they've stopped talking" timer
                    _ = EndAfterSilenceAsync();
                }

                if (error is not null)
                    done.TrySetResult(lastWords is null ? VoiceSearchResult.Nothing : new VoiceSearchResult(lastWords.Trim()));
            });
        });

        using var longest = new CancellationTokenSource(Longest);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(ct, longest.Token);
        await using var stop = cancel.Token.Register(() =>
        {
            if (ct.IsCancellationRequested) done.TrySetResult(VoiceSearchResult.Nothing);
            else Stop();   // 10 seconds: use what was heard
        });

        try
        {
            return await done.Task;
        }
        finally
        {
            await MainThread.InvokeOnMainThreadAsync(TearDown);
        }

        // Ends listening when no new words came for a moment (each new word restarts it)
        async Task EndAfterSilenceAsync()
        {
            var heard = lastWords;
            await Task.Delay(Silence);
            if (heard == lastWords) Stop();
        }
    }

    /// <inheritdoc />
    public void Stop() => MainThread.BeginInvokeOnMainThread(() =>
    {
        _engine?.Stop();
        _engine?.InputNode.RemoveTapOnBus(0);
        _request?.EndAudio();   // the task then sends its final result
    });

    /// <summary>Releases the microphone and the recognition task.</summary>
    private void TearDown()
    {
        _task?.Cancel();
        _task = null;
        if (_engine is not null)
        {
            if (_engine.Running) _engine.Stop();
            _engine.InputNode.RemoveTapOnBus(0);
            _engine.Dispose();
            _engine = null;
        }
        _request?.Dispose();
        _request = null;
        AVAudioSession.SharedInstance().SetActive(false, AVAudioSessionSetActiveOptions.NotifyOthersOnDeactivation, out _);
    }

    /// <summary>Asks for speech-recognition permission (once; later calls just read the answer).</summary>
    private static Task<SFSpeechRecognizerAuthorizationStatus> RequestSpeechPermissionAsync()
    {
        var answer = new TaskCompletionSource<SFSpeechRecognizerAuthorizationStatus>();
        SFSpeechRecognizer.RequestAuthorization(status => answer.TrySetResult(status));
        return answer.Task;
    }
}
