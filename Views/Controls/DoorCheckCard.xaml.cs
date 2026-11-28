using Phloem.Models;
using Phloem.Services;

namespace Phloem.Views.Controls;

/// <summary>Door safety card; set <see cref="Check"/> (hidden while it's null).</summary>
[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class DoorCheckCard : ContentView
{
    public DoorCheckCard()
    {
        InitializeComponent();
    }

    public static readonly BindableProperty CheckProperty =
        BindableProperty.Create(nameof(Check), typeof(DoorCheckView), typeof(DoorCheckCard), null,
            propertyChanged: (bindable, _, value) => ((DoorCheckCard)bindable).IsVisible = value is not null);

    /// <summary>Who is coming, the door word and PIN; null = nothing to show.</summary>
    public DoorCheckView? Check
    {
        get => (DoorCheckView?)GetValue(CheckProperty);
        set => SetValue(CheckProperty, value);
    }

    /// <summary>"This isn't my courier": tell them not to open, ask what doesn't match, alert Phloem.</summary>
    private async void OnConcernClicked(object? sender, EventArgs e)
    {
        if (Check is not { } check) return;
        try
        {
            var note = await Shell.Current.DisplayPromptAsync("Don't open the door",
                $"Phloem staff will be alerted at once. If you feel in danger, call {check.EmergencyNumber} now.\n\n" +
                "What doesn't match? (optional)", "Alert Phloem", "It's fine", "e.g. Different person", 500);
            if (note is null) return;   // "It's fine"

            var couriers = IPlatformApplication.Current?.Services.GetService<CourierService>();
            if (couriers is null) return;

            ConcernButton.IsEnabled = false;
            var result = await couriers.RaiseConcernAsync(check.Kind, check.Reference, note);
            AlertedText.Text = result.IsSuccess
                ? result.Value!.Message
                : $"{result.Error!.DisplayMessage} If you feel in danger, call {check.EmergencyNumber}.";
            AlertedBox.IsVisible = true;
            ConcernButton.IsVisible = !result.IsSuccess;
        }
        catch (Exception)
        {
            AlertedText.Text = $"We couldn't reach Phloem. Don't open the door; if you feel in danger, call {check.EmergencyNumber}.";
            AlertedBox.IsVisible = true;
        }
        finally
        {
            ConcernButton.IsEnabled = true;
        }
    }
}
