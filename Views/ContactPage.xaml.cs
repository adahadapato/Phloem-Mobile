using Phloem.ViewModels;

namespace Phloem.Views;

public partial class ContactPage : ContentPage
{
    readonly ContactViewModel _vm;

    public ContactPage(ContactViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // Who's signed in, and the contact details
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.LoadAsync(); }
        catch (Exception) { /* the form still works; the details card stays hidden */ }
    }

    async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
