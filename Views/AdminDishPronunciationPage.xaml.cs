using Phloem.ViewModels;

namespace Phloem.Views;

/// <summary>Admin → Dish names → Pronunciation (route "admindishsound").</summary>
public partial class AdminDishPronunciationPage : ContentPage
{
    readonly AdminDishPronunciationViewModel _vm;

    public AdminDishPronunciationPage(AdminDishPronunciationViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override async void OnDisappearing()
    {
        base.OnDisappearing();
        try { await _vm.LeaveAsync(); }
        catch (Exception) { /* nothing to undo */ }
    }

    async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
