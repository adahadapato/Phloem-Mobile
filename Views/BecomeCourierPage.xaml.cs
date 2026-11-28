using Phloem.ViewModels;

namespace Phloem.Views;

public partial class BecomeCourierPage : ContentPage
{
    readonly BecomeCourierViewModel _vm;

    public BecomeCourierPage(BecomeCourierViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // Fresh every time (staff may have decided since)
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.LoadAsync(); }
        catch (Exception) { /* the page shows what it has; pulling back in retries */ }
    }

    async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
