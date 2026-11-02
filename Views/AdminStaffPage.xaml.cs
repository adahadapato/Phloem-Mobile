using Phloem.ViewModels;

namespace Phloem.Views;

/// <summary>Admin → Staff (route "adminstaff"), read-only. See <see cref="AdminStaffViewModel"/>.</summary>
public partial class AdminStaffPage : ContentPage
{
    readonly AdminStaffViewModel _vm;

    public AdminStaffPage(AdminStaffViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    // The list as it is now
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.LoadAsync(); }
        catch (Exception) { /* the page says what went wrong; pull down to try again */ }
    }

    async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
