using Phloem.Models;

namespace Phloem.Views.Controls;

/// <summary>The "Proof of delivery" card; set <see cref="Proof"/> (hidden while it's null).</summary>
[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class DeliveryProofCard : ContentView
{
    public DeliveryProofCard()
    {
        InitializeComponent();
    }

    public static readonly BindableProperty ProofProperty =
        BindableProperty.Create(nameof(Proof), typeof(DeliveryProofView), typeof(DeliveryProofCard), null,
            propertyChanged: (bindable, _, value) => ((DeliveryProofCard)bindable).IsVisible = value is not null);

    /// <summary>The photo and details; null = nothing to show.</summary>
    public DeliveryProofView? Proof
    {
        get => (DeliveryProofView?)GetValue(ProofProperty);
        set => SetValue(ProofProperty, value);
    }

    /// <summary>Opens the photo full size (the phone's browser or photo viewer).</summary>
    private async void OnPhotoTapped(object? sender, TappedEventArgs e)
    {
        if (Proof is null) return;
        try { await Launcher.Default.OpenAsync(new Uri(Proof.PhotoUrl)); }
        catch (Exception) { /* nothing can open it: the photo is still on the card */ }
    }
}
