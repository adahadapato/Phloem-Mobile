using Phloem.Services.Api.Contracts;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Models;

/// <summary>A shop card on the admin dashboard (pending, or the full list), with its logo.</summary>
public sealed class AdminShopCard
{
    public required AdminShopDto Shop { get; init; }

    /// <summary>The shop's logo (address resolved for this device), or the Phloem logo when it has none.</summary>
    public required string LogoUrl { get; init; }

    public string Name => Shop.Name;
    public string OwnerText => $"Owner: {Shop.OwnerName}";
    public string CountryText => ShipmentText.CountryName(Shop.Country);
    public string EmailText => Shop.BusinessEmail;
    public string OpenedText => $"Opened {Shop.OpenedAt.ToLocalTime():d MMM yyyy}";
    public string RatingText => Shop.RatingCount == 0 ? "No ratings yet" : $"★ {Shop.Rating:0.0} ({Shop.RatingCount})";

    public bool IsPending => Shop.Status == VendorStatus.PendingApproval;
    public bool IsBlocked => Shop.Status == VendorStatus.Blocked;
    public bool IsVerified => Shop.IsVerified;
    public bool IsOwnerSuspended => Shop.IsOwnerSuspended;

    /// <summary>"Live", "Blocked", "Rejected: …", "Owner suspended"…</summary>
    public string StatusText => Shop.IsOwnerSuspended ? "Owner suspended" : Shop.Status switch
    {
        VendorStatus.PendingApproval => "Waiting for approval",
        VendorStatus.Approved => "Live",
        VendorStatus.Blocked => "Blocked",
        VendorStatus.Rejected => "Rejected",
        VendorStatus.Closed => "Closed",
        _ => Shop.Status.ToString()
    };

    /// <summary>Red chip for blocked / rejected / owner suspended.</summary>
    public bool IsBad => Shop.IsOwnerSuspended || Shop.Status is VendorStatus.Blocked or VendorStatus.Rejected;

    public string BlockButtonText => IsBlocked ? "Unblock" : "Block";
    public string VerifyButtonText => IsVerified ? "Remove ✓" : "Give ✓";

    /// <summary>Block and ✓ only make sense for shops that were approved at some point (not pending / closed).</summary>
    public bool CanManage => Shop.Status is VendorStatus.Approved or VendorStatus.Blocked;
}

/// <summary>A person on Admin → Users, with their suspension.</summary>
public sealed class AdminUserCard
{
    public required AdminUserDto User { get; init; }

    /// <summary>Their profile photo, ready to show (null = the person icon).</summary>
    public string? PhotoUrl { get; init; }
    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoUrl);
    public bool HasNoPhoto => !HasPhoto;

    public string FullName => User.IsYou ? $"{User.FullName} (you)" : User.FullName;
    public bool IsYou => User.IsYou;
    public string Email => User.Email;
    public string PhoneText => User.PhoneNumber ?? string.Empty;
    public bool HasPhone => !string.IsNullOrWhiteSpace(User.PhoneNumber);
    public string RolesText => string.Join(" · ", User.Roles.OrderBy(r => r).Select(Roles.DisplayName));

    /// <summary>True when they have this role (any letter case).</summary>
    public bool Has(string role) => User.Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    public string DetailsText =>
        $"{ShipmentText.CountryName(User.Country)}  ·  Joined {User.MemberSince.ToLocalTime():d MMM yyyy}";
    public string? ShopText => User.ShopName is null ? null : $"Shop: {User.ShopName}";
    public bool HasShop => User.ShopName is not null;

    public bool IsSuspended => User.IsSuspended;

    /// <summary>"Suspended until 28 Oct 2026: fake orders" / "Suspended until further notice: …".</summary>
    public string SuspensionText => !User.IsSuspended ? string.Empty :
        (User.SuspendedUntil is { } until ? $"Suspended until {until.ToLocalTime():d MMM yyyy}" : "Suspended until further notice") +
        (string.IsNullOrWhiteSpace(User.SuspensionReason) ? string.Empty : $": {User.SuspensionReason}");

    public bool ShowSuspend => User.CanBeSuspended && !User.IsSuspended;
    public bool ShowLift => User.IsSuspended;
}

/// <summary>A dish name a seller suggested.</summary>
public sealed class DishSuggestionCard
{
    public required AdminDishSuggestionDto Suggestion { get; init; }

    public string Name => Suggestion.Name;
    public string DetailsText =>
        $"{Suggestion.Category}  ·  suggested by {Suggestion.SuggestedBy}" +
        (Suggestion.ShopName is null ? string.Empty : $" ({Suggestion.ShopName})");
}
