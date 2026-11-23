using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Services;
using Phloem.Services.Api.Contracts.Commands;

namespace Phloem.ViewModels;

/// <summary>
/// Change password (route "changepassword"), saved to the API (<c>PUT /api/me/password</c>).
/// The API then signs the user out on EVERY device, so this screen signs out here too and opens Log in.
/// </summary>
public partial class ChangePasswordViewModel : ObservableObject
{
    private readonly SessionService _session;
    private readonly AccountService _account;
    private readonly AuthService _auth;
    private bool _saving;

    public ChangePasswordViewModel(SessionService session, AccountService account, AuthService auth)
    {
        _session = session;
        _account = account;
        _auth = auth;
    }

    [ObservableProperty]
    public partial string CurrentPassword { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMinLength), nameof(HasNumber), nameof(HasLetter))]
    public partial string NewPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ConfirmPassword { get; set; } = string.Empty;

    /// <summary>The eye icon: shows / hides all three boxes.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HidePasswords))]
    public partial bool ShowPasswords { get; set; }

    public bool HidePasswords => !ShowPasswords;

    // Live checklist under "New password"
    public bool HasMinLength => (NewPassword ?? string.Empty).Length >= 8;
    public bool HasNumber    => (NewPassword ?? string.Empty).Any(char.IsDigit);
    public bool HasLetter    => (NewPassword ?? string.Empty).Any(char.IsLetter);

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsError { get; set; }

    /// <summary>Called by the page each time it appears: start with empty boxes.</summary>
    public void Reset()
    {
        CurrentPassword = NewPassword = ConfirmPassword = string.Empty;
        ShowPasswords = false;
        StatusMessage = string.Empty;
        IsError = false;
    }

    [RelayCommand]
    private void ToggleShow() => ShowPasswords = !ShowPasswords;

    [RelayCommand]
    private async Task Save()
    {
        if (_saving || !_session.IsLoggedIn) return;

        // Quick checks on the phone; the API checks the current password and its own rules
        string? problem =
            string.IsNullOrEmpty(CurrentPassword)        ? "Enter your current password." :
            !HasMinLength || !HasNumber || !HasLetter    ? "Your new password needs at least 8 characters, with letters and a number." :
            NewPassword == CurrentPassword               ? "Your new password must be different from the current one." :
            NewPassword != ConfirmPassword               ? "The two new passwords don't match." :
            null;

        if (problem is not null)
        {
            IsError = true;
            StatusMessage = problem;
            return;
        }

        _saving = true;
        try
        {
            var result = await _account.ChangePasswordAsync(
                new ChangePasswordCommand(CurrentPassword, NewPassword, ConfirmPassword));
            if (result.IsFailure)
            {
                IsError = true;
                StatusMessage = result.Error!.DisplayMessage;   // e.g. "Your current password isn't right."
                return;
            }

            // The API has signed this account out everywhere: sign out here too, then Log in
            Reset();
            await Shell.Current.DisplayAlertAsync("Password changed",
                "For your security you've been signed out on all your devices. Please log in with your new password.",
                "Log in");
            await _auth.LogoutAsync(forgetThisPhone: true);   // the old sign-in is dead: nothing to unlock
            await Shell.Current.Navigation.PopToRootAsync(false);
            await Shell.Current.GoToAsync("//account/login");
        }
        finally
        {
            _saving = false;
        }
    }
}
