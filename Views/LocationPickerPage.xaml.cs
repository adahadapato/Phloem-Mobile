using Phloem.Models;
using Phloem.ViewModels;

namespace Phloem.Views;

/// <summary>
/// Full-screen State / LGA list (see <see cref="LocationPickerViewModel"/>). Not a Shell route: it's opened over the
/// current page by <see cref="PickAsync"/>, which waits for a tap (or Close / Back) and returns the picked option.
/// </summary>
public partial class LocationPickerPage : ContentPage
{
    private readonly TaskCompletionSource<LocationOption?> _picked = new();

    private LocationPickerPage(LocationPickerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }

    /// <summary>Shows the list and waits; null when closed without picking.</summary>
    /// <param name="title">e.g. "Choose the state".</param>
    /// <param name="options">The states or LGAs.</param>
    /// <param name="selectedId">The one picked before, if any.</param>
    public static async Task<LocationOption?> PickAsync(string title, IReadOnlyList<LocationOption> options, Guid? selectedId)
    {
        var page = new LocationPickerPage(new LocationPickerViewModel(title, options, selectedId));
        await Shell.Current.Navigation.PushModalAsync(page);
        return await page._picked.Task;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Search.Focus();
    }

    private async void OnOptionTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not LocationOption option || _picked.Task.IsCompleted) return;
        _picked.TrySetResult(option);
        await Navigation.PopModalAsync();
    }

    private async void OnCloseTapped(object? sender, TappedEventArgs e)
    {
        _picked.TrySetResult(null);
        await Navigation.PopModalAsync();
    }

    /// <summary>Android back button: close without picking.</summary>
    protected override bool OnBackButtonPressed()
    {
        _picked.TrySetResult(null);
        return base.OnBackButtonPressed();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _picked.TrySetResult(null);   // closed some other way
    }
}
