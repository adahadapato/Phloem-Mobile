using Phloem.ViewModels;

namespace Phloem.Views;

public partial class AdminPricingPage : ContentPage
{
    readonly AdminPricingViewModel _vm;

    public AdminPricingPage(AdminPricingViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
        _vm.FormOpened += OnFormOpened;
    }

    // The prices as they are now
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try { await _vm.LoadAsync(); }
        catch (Exception) { /* the page says what went wrong; pull down to try again */ }
    }

    // "Change" on a card further down: bring the form (at the top) into view and put the cursor in it
    async void OnFormOpened(object? sender, EventArgs e)
    {
        try
        {
            await Scroller.ScrollToAsync(0, 0, true);
            FirstEntry.Focus();
        }
        catch (Exception) { /* scrolling is only a nicety */ }
    }

    async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
