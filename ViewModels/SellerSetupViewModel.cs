using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Services;
using Phloem.Services.Api.Contracts.Commands;

namespace Phloem.ViewModels;

/// <summary>
/// Become a Seller (route "sellersetup"), on the API:
/// <list type="number">
///   <item><description>Signed in, with personal details (date of birth + home address; sellers must be 18+).</description></item>
///   <item><description>Shop name, and optionally a business email, phone and logo.</description></item>
///   <item><description>Submit: the shop waits for Phloem's approval; next stop Shop settings to upload the
///   documents.</description></item>
/// </list>
/// </summary>
public partial class SellerSetupViewModel : ObservableObject
{
    private readonly SessionService _session;
    private readonly SellerService _seller;
    private readonly AuthService _auth;
    private FileResult? _logo;
    private bool _busy;

    public SellerSetupViewModel(SessionService session, SellerService seller, AuthService auth)
    {
        _session = session;
        _seller = seller;
        _auth = auth;
    }

    [ObservableProperty]
    public partial string BusinessName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BusinessEmail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string BusinessPhone { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string LogoFileName { get; set; } = string.Empty;

    /// <summary>"I agree to the Seller terms" (required to open a shop; recorded as proof).</summary>
    [ObservableProperty]
    public partial bool AgreedToSellerTerms { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsError { get; set; }

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    public bool IsLoggedIn => _session.IsLoggedIn;

    /// <summary>Signed in but no date of birth / home address yet: they're needed before opening a shop.</summary>
    public bool NeedsPersonalDetails => _session.IsLoggedIn && _session.Profile?.HasPersonalDetails != true;

    /// <summary>Signed in, with personal details, and no shop yet: the form can be sent.</summary>
    public bool CanOpenShop => _session.IsLoggedIn && !NeedsPersonalDetails && !_session.HasShop;

    public string LoggedInAsText => _session.IsLoggedIn
        ? $"Setting up a seller account for {_session.CurrentUser!.FullName} ({_session.CurrentUser!.Email})"
        : string.Empty;

    /// <summary>
    /// Called by the page each time it appears, so the notices are right after logging in, signing up or adding
    /// personal details. Someone who already has a shop goes to their Seller dashboard.
    /// </summary>
    public async void RefreshSession()
    {
        foreach (var name in new[] { nameof(IsLoggedIn), nameof(LoggedInAsText), nameof(NeedsPersonalDetails),
                                     nameof(CanOpenShop) })
            OnPropertyChanged(name);

        if (_session.HasShop && !_busy)
        {
            try { await Shell.Current.GoToAsync("//home/dashboard"); }
            catch (Exception) { /* stays here; the menu has Seller Dashboard */ }
        }
    }

    [RelayCommand]
    private async Task GoToLogin() => await Shell.Current.GoToAsync("//account/login");

    /// <summary>"Add personal details" → My account → Edit profile.</summary>
    [RelayCommand]
    private async Task AddPersonalDetails() => await Shell.Current.GoToAsync("//account/editprofile");

    [RelayCommand]
    private async Task PickLogo()
    {
        try
        {
            var picked = (await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions { SelectionLimit = 1 }))
                ?.FirstOrDefault();
            if (picked is null) return;
            _logo = picked;
            LogoFileName = picked.FileName;
        }
        catch (Exception)
        {
            // no media picker on this device: the logo can be added later in Shop settings
        }
    }

    [RelayCommand]
    private async Task CreateVendorProfile()
    {
        if (_busy) return;
        if (!_session.IsLoggedIn)
        {
            ShowError("Please log in first: a shop is linked to your Phloem account.");
            return;
        }
        if (NeedsPersonalDetails)
        {
            ShowError("Please add your date of birth and home address first (My account → Edit profile).");
            return;
        }
        if (string.IsNullOrWhiteSpace(BusinessName))
        {
            ShowError("Please enter your shop's name.");
            return;
        }
        if (!AgreedToSellerTerms)
        {
            ShowError("Please read and agree to the Seller terms to open your shop.");
            return;
        }

        _busy = true;
        IsLoading = true;
        try
        {
            var email = string.IsNullOrWhiteSpace(BusinessEmail) ? null : BusinessEmail.Trim();
            var phone = string.IsNullOrWhiteSpace(BusinessPhone) ? null : BusinessPhone.Trim();
            var opened = await _seller.OpenShopAsync(new OpenShopCommand(BusinessName.Trim(), email, phone, AgreedToSellerTerms));
            if (opened.IsFailure)
            {
                ShowError(opened.Error!.DisplayMessage);
                return;
            }

            // The logo (optional): the shop is open even if this fails; it can be added in Shop settings
            if (_logo is not null)
            {
                try
                {
                    await using var stream = await _logo.OpenReadAsync();
                    var type = string.IsNullOrWhiteSpace(_logo.ContentType) ? "image/jpeg" : _logo.ContentType;
                    await _seller.ChangeLogoAsync(stream, _logo.FileName, type);
                }
                catch (Exception) { /* add it later in Shop settings */ }
            }

            await _auth.RefreshProfileAsync();   // the menu now shows Seller Dashboard

            await Shell.Current.DisplayAlertAsync("Shop submitted",
                $"{BusinessName.Trim()} is waiting for Phloem's approval. Next, upload your documents in Shop settings " +
                "so Phloem can check them.", "Go to Shop settings");
            await Shell.Current.GoToAsync("//home/dashboard/shopsettings");   // back = the dashboard
        }
        finally
        {
            _busy = false;
            IsLoading = false;
        }
    }

    private void ShowError(string message)
    {
        IsError = true;
        StatusMessage = message;
    }
}
