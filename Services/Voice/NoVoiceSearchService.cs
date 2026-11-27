namespace Phloem.Services.Voice;

/// <summary>Windows / Mac: no voice search (the 🎤 stays hidden).</summary>
public sealed class NoVoiceSearchService : IVoiceSearchService
{
    /// <inheritdoc />
    public bool IsSupported => false;

    /// <inheritdoc />
    public Task<VoiceSearchResult> ListenAsync(string locale, IReadOnlyCollection<string> hints, Action<string> onWords,
                                               CancellationToken ct) =>
        Task.FromResult(VoiceSearchResult.Nothing);

    /// <inheritdoc />
    public void Stop() { }
}
