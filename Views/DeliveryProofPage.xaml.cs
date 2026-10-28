using Phloem.ViewModels;

namespace Phloem.Views;

public partial class DeliveryProofPage : ContentPage
{
    readonly DeliveryProofViewModel _vm;

    public DeliveryProofPage(DeliveryProofViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // The first time: straight to the camera
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.OnAppearingAsync(); }
        catch (Exception) { /* the "Take a photo" button is still there */ }
    }

    async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
