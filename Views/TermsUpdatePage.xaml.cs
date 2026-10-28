using Phloem.ViewModels;

namespace Phloem.Views;

public partial class TermsUpdatePage : ContentPage
{
    readonly TermsUpdateViewModel _vm;

    public TermsUpdatePage(TermsUpdateViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Load();
    }

    // Android back button = "Not now"
    protected override bool OnBackButtonPressed()
    {
        _vm.NotNowCommand.Execute(null);
        return true;
    }
}
