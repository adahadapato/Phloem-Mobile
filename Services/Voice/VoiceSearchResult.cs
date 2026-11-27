namespace Phloem.Services.Voice;

/// <summary>How one listen ended.</summary>
/// <param name="Text">What was heard (the best guess), or null when nothing was.</param>
/// <param name="Problem">A message to show (no microphone permission, no connection…); null when there's nothing to
/// say (heard something, or the person stopped it).</param>
public sealed record VoiceSearchResult(string? Text, string? Problem = null)
{
    /// <summary>Nothing heard and nothing to say (cancelled, or silence).</summary>
    public static VoiceSearchResult Nothing { get; } = new(null);
}
