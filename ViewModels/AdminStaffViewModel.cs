using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Phloem.Models;
using Phloem.Services;
using Phloem.Services.Api.Contracts.Dtos;

namespace Phloem.ViewModels;

/// <summary>One person on the Staff page, ready to show.</summary>
public sealed class StaffCard
{
    /// <summary>The person as the API sent them.</summary>
    public required StaffMemberDto Member { get; init; }

    /// <summary>"Ama Mensah (you)".</summary>
    public string FullName => Member.IsYou ? $"{Member.FullName} (you)" : Member.FullName;

    /// <summary>"A".</summary>
    public string Initial => string.IsNullOrEmpty(Member.FullName) ? "?" : Member.FullName[..1].ToUpperInvariant();

    /// <summary>Their email.</summary>
    public string Email => Member.Email;

    /// <summary>"Admin (owner)", or their job roles, e.g. "Parcel hub · Support".</summary>
    public string RolesText => Member.IsAdmin
        ? "Admin (owner)"
        : Member.JobRoleNames.Count == 0 ? "No job role" : string.Join(" · ", Member.JobRoleNames);

    /// <summary>"🌍 All countries", or e.g. "🇬🇭 Ghana · 🇳🇬 Nigeria".</summary>
    public string CountriesText => Member.CoversAllCountries
        ? "🌍 All countries"
        : Member.Countries.Count == 0 ? "No country yet" : string.Join(" · ", Member.Countries.Select(ShipmentText.CountryName));

    /// <summary>"Last sign-in 2 Nov 2026, 06:40", or "Never signed in".</summary>
    public string LastSignInText => Member.LastSignIn is { } at
        ? $"Last sign-in {at.ToLocalTime():d MMM yyyy, HH:mm}"
        : "Never signed in";

    /// <summary>Two-step sign-in is required for all staff; flagged when it's off.</summary>
    public bool TwoFactorOff => !Member.TwoFactorEnabled;

    /// <summary>"2-step on" / "2-step off".</summary>
    public string TwoFactorText => Member.TwoFactorEnabled ? "2-step on" : "2-step off";

    /// <summary>True while suspended.</summary>
    public bool IsSuspended => Member.IsSuspended;
}

/// <summary>
/// Admin → Staff (route "adminstaff"): the owners and everyone with a job role, read-only. Changing job roles,
/// countries and what a role may do is on the website (Staff area → Staff).
/// </summary>
public partial class AdminStaffViewModel(StaffService staff) : ObservableObject
{
    /// <summary>The people, owners first.</summary>
    public ObservableCollection<StaffCard> People { get; } = new();

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    public partial string Message { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsError { get; set; }

    /// <summary>"5 people".</summary>
    [ObservableProperty]
    public partial string CountText { get; set; } = string.Empty;

    /// <summary>Loads the list.</summary>
    public async Task LoadAsync()
    {
        IsLoading = true;
        try
        {
            var result = await staff.GetStaffAsync();
            if (result.IsFailure)
            {
                IsError = true;
                Message = result.Error!.DisplayMessage;
                return;
            }

            People.Clear();
            foreach (var member in result.Value!) People.Add(new StaffCard { Member = member });
            CountText = People.Count == 1 ? "1 person" : $"{People.Count} people";
            IsError = false;
            Message = string.Empty;
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Pull to refresh.</summary>
    [RelayCommand]
    private async Task Refresh() => await LoadAsync();
}
