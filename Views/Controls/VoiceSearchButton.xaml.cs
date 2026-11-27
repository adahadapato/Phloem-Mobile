using System.Windows.Input;

namespace Phloem.Views.Controls;

/// <summary>
/// The 🎤 voice-search button (Phloem style: green and gold, with a ring that pulses while listening).
/// </summary>
/// <remarks>
/// Tapping runs <see cref="Command"/>; the view model sets <see cref="IsListening"/> while the phone listens, which
/// turns the button green and starts the ring's pulse (stops when it's set back to false).
/// </remarks>
[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class VoiceSearchButton : ContentView
{
    private const string PulseAnimation = "VoicePulse";

    /// <summary>Builds the button.</summary>
    public VoiceSearchButton()
    {
        InitializeComponent();
    }

    /// <summary>Runs when tapped (start listening, or stop when already listening).</summary>
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(VoiceSearchButton));

    /// <inheritdoc cref="CommandProperty"/>
    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    /// <summary>True while the phone is listening: green button, pulsing ring.</summary>
    public static readonly BindableProperty IsListeningProperty =
        BindableProperty.Create(nameof(IsListening), typeof(bool), typeof(VoiceSearchButton), false,
            propertyChanged: (bindable, _, value) => ((VoiceSearchButton)bindable).ShowListening((bool)value));

    /// <inheritdoc cref="IsListeningProperty"/>
    public bool IsListening
    {
        get => (bool)GetValue(IsListeningProperty);
        set => SetValue(IsListeningProperty, value);
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (Command?.CanExecute(null) == true) Command.Execute(null);
    }

    /// <summary>Switches between idle and listening.</summary>
    private void ShowListening(bool listening)
    {
        this.AbortAnimation(PulseAnimation);
        SemanticProperties.SetDescription(this, listening ? "Listening. Tap to stop" : "Search by voice");

        if (!listening)
        {
            Disc.Fill = new SolidColorBrush(Colors.Transparent);
            Mic.Stroke = new SolidColorBrush(Brand("BrandTextDark", Color.FromArgb("#070F19")));
            Ring.Opacity = 0;
            Ring.Scale = 1;
            return;
        }

        Disc.Fill = new SolidColorBrush(Brand("BrandGreen", Color.FromArgb("#046334")));
        Mic.Stroke = new SolidColorBrush(Colors.White);

        // The ring grows from the button and fades, over and over, while listening
        new Animation(progress =>
        {
            Ring.Scale = 0.8 + 0.45 * progress;
            Ring.Opacity = 0.9 * (1 - progress);
        }).Commit(this, PulseAnimation, length: 1100, easing: Easing.CubicOut, repeat: () => IsListening);
    }

    /// <summary>A colour from Colors.xaml (or the fallback if it can't be found).</summary>
    private static Color Brand(string key, Color fallback) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color ? color : fallback;
}
