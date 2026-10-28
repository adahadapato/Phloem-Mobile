using Phloem.ViewModels;

namespace Phloem.Views;

public partial class MyDeliveriesPage : ContentPage
{
    readonly MyDeliveriesViewModel _vm;

    public MyDeliveriesPage(MyDeliveriesViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // Every time: a delivery may have been added, or just marked delivered
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.LoadAsync(); }
        catch (Exception) { /* the message banner already says what went wrong */ }
    }

    async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
