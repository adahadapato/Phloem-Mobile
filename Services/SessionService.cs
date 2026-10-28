using Phloem.Models;
using Phloem.Services.Api;
using Phloem.Services.Api.Contracts;
using Phloem.Services.Api.Contracts.Dtos;

namespace Phloem.Services;

/// <summary>
/// Who is signed in, for every screen: the profile from the API (<see cref="Profile"/>), the same person as the app's
/// <see cref="User"/> model (<see cref="CurrentUser"/>, which the existing screens bind to), and their roles.
/// </summary>
/// <remarks>
/// Filled by <c>AuthService</c> after sign-in; emptied on sign-out and when the sign-in can't be renewed
/// (<see cref="TokenStore.SessionExpired"/>). Screens can listen to <see cref="Changed"/> to refresh.
/// </remarks>
public class SessionService
{
    private IReadOnlyCollection<string> _roles = [];

    /// <summary>Listens for "the sign-in ran out" so the app shows the user as signed out.</summary>
    public SessionService(TokenStore tokens)
    {
        tokens.SessionExpired += (_, _) => MainThread.BeginInvokeOnMainThread(SignOut);
    }

    /// <summary>
    /// The signed-in user, as the existing screens use it; null when signed out.
    /// Filled by <c>AuthService</c>. To sign out, call <c>AuthService.LogoutAsync</c> (it also cancels the tokens);
    /// setting this to null only hides the user on this screen until the app restarts.
    /// </summary>
    public User? CurrentUser { get; set; }

    /// <summary>The signed-in user's full profile from the API; null when signed out.</summary>
    public ProfileDto? Profile { get; private set; }

    /// <summary>Raised after sign-in, sign-out and profile reloads.</summary>
    public event EventHandler? Changed;

    /// <summary>True when someone is signed in.</summary>
    public bool IsLoggedIn => CurrentUser is not null;

    /// <summary>True for admins.</summary>
    public bool IsAdmin => IsInRole(Roles.Admin);

    /// <summary>True once an admin has approved the user's shop.</summary>
    public bool IsSeller => IsInRole(Roles.Seller);

    /// <summary>True for couriers (they see "My deliveries").</summary>
    public bool IsCourier => IsInRole(Roles.Courier);

    /// <summary>True when the user has opened a shop (approved or not).</summary>
    public bool HasShop => Profile?.HasShop ?? false;

    /// <summary>Checks a role from the sign-in token.</summary>
    public bool IsInRole(string role) => IsLoggedIn && _roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    /// <summary>Sets the signed-in user (called by <c>AuthService</c>).</summary>
    /// <param name="profile">The profile from <c>GET /api/me</c>.</param>
    /// <param name="roles">The roles from the sign-in token.</param>
    public void SignIn(ProfileDto profile, IReadOnlyCollection<string> roles)
    {
        Profile = profile;
        _roles = roles;
        CurrentUser = ToUser(profile, roles);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Forgets the signed-in user (tokens are cleared by <c>AuthService</c> / <see cref="TokenStore"/>).</summary>
    public void SignOut()
    {
        if (CurrentUser is null && Profile is null) return;

        Profile = null;
        _roles = [];
        CurrentUser = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>The API's profile as the app's <see cref="User"/> model.</summary>
    private static User ToUser(ProfileDto profile, IReadOnlyCollection<string> roles) => new()
    {
        Id = profile.Id.ToString(),
        FullName = profile.FullName,
        Email = profile.Email,
        PhoneNumber = profile.PhoneNumber ?? string.Empty,
        Country = CountryOption.NameOf(profile.Country),
        JoinedAt = profile.MemberSince.ToLocalTime(),
        IsAdmin = roles.Contains(Roles.Admin, StringComparer.OrdinalIgnoreCase),
        PhotoPath = profile.PhotoUrl,
        NotifyOrderUpdates = profile.NotifyOrderUpdates,
        NotifyShipmentUpdates = profile.NotifyShipmentUpdates,
        NotifyOffers = profile.NotifyOffers,
        NotifyNewDishes = profile.NotifyNewDishes,
        NotifyShopOrders = profile.NotifyShopOrders,
        Addresses = profile.Addresses.Select(a => new DeliveryAddress
        {
            Id = a.Id.ToString(),
            Label = a.Label,
            FullAddress = a.FullAddress,
            Country = CountryOption.NameOf(a.Country),
            IsDefault = a.IsDefault,
            RecipientName = a.RecipientName,
            PhoneNumber = a.PhoneNumber
        }).ToList()
    };
}
