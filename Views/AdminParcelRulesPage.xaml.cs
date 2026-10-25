using Phloem.ViewModels;

namespace Phloem.Views;

public partial class AdminParcelRulesPage : ContentPage
{
    readonly AdminParcelRulesViewModel _vm;

    public AdminParcelRulesPage(AdminParcelRulesViewModel vm)
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
