using System.Windows.Input;

namespace Phloem.Services.Pronunciation;

/// <summary>
/// The 🔊 on dish cards (Home, Explore, suggestion rows): one shared command, used from XAML as
/// <c>Command="{x:Static pron:SayName.PlayCommand}" CommandParameter="{Binding PronunciationAudioUrl}"</c>, so every
/// card template can play a dish name without its page's view model knowing about it (like <c>AppNav.GoHomeCommand</c>).
/// </summary>
public static class SayName
{
    /// <summary>The one player (also registered in DI, so the seller picker and admin pages share it).</summary>
    public static PronunciationPlayer Player { get; } = new();

    /// <summary>Plays the recording at the link given as the parameter (nothing happens if it can't play).</summary>
    public static ICommand PlayCommand { get; } = new Command<string?>(async url => await Player.PlayAsync(url));
}
