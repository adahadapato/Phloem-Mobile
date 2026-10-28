using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Services;

namespace Phloem.ViewModels;

/// <summary>
/// "We've updated our Terms & Conditions" (route "termsupdate", shown modally): read them, then Accept, or "Not now" to
/// keep browsing (ordering, sending parcels and selling wait). The same as the website's box.
/// </summary>
public partial class TermsUpdateViewModel(SessionService session, AccountService account, AuthService auth,
                                          TermsPromptService prompt) : ObservableObject
{
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Text { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsError { get; set; }

    private int Version => session.Profile?.Terms?.CurrentTermsVersion ?? 0;

    /// <summary>Fills the wording from the profile (called when the screen appears).</summary>
    public void Load()
    {
        var firstTime = session.Profile?.Terms?.AcceptedTermsVersion is null;
        Title = firstTime ? "Please accept our Terms & Conditions" : "We've updated our Terms & Conditions";
        Text = firstTime
            ? "Before you order, send a parcel or sell on Phloem, please read and accept our Terms & Conditions."
            : $"Version {Version} is now in effect. Please read it and accept it to keep ordering, sending parcels and selling.";
    }

    [RelayCommand]
    private async Task ReadTerms() => await AppNav.OpenLegalAsync("Terms");

    [RelayCommand]
    private async Task Accept()
    {
        if (IsBusy || Version == 0) return;
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var result = await account.AcceptTermsAsync(Version);
            if (result.IsFailure)
            {
                IsError = true;
                StatusMessage = result.Error!.DisplayMessage;
                if (result.Error.Code == "Terms.Outdated")
                {
                    await auth.RefreshProfileAsync();   // shows the newer version
                    Load();
                }
                return;
            }

            await auth.RefreshProfileAsync();
            await Shell.Current.GoToAsync("..");
            await Shell.Current.DisplayAlertAsync("Thank you", "You can order, send parcels and sell again.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task NotNow()
    {
        prompt.DismissedVersion = Version;   // not asked again until the app restarts
        await Shell.Current.GoToAsync("..");
    }
}
