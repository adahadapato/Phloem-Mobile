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

        if (!listening)
        {
            Disc.Fill = new SolidColorBrush(Colors.Transparent);
            Mic.Stroke = new SolidColorBrush((Color)Application.Current!.Resources["BrandTextDark"]);
            Ring.Opacity = 0;
            Ring.Scale = 1;
            SemanticScreenReader.Announce("Stopped listening");
            return;
        }

        Disc.Fill = new SolidColorBrush((Color)Application.Current!.Resources["BrandGreen"]);
        Mic.Stroke = new SolidColorBrush(Colors.White);
        SemanticScreenReader.Announce("Listening");

        // The ring grows from the button and fades, over and over, while listening
        new Animation(progress =>
        {
            Ring.Scale = 0.8 + 0.45 * progress;
            Ring.Opacity = 0.9 * (1 - progress);
        }).Commit(this, PulseAnimation, length: 1100, easing: Easing.CubicOut, repeat: () => IsListening);
    }
}
