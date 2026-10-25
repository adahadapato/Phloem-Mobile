using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>
/// One person on Admin → Users. <see cref="CanBeSuspended"/> is false for admins (and yourself); <see cref="IsYou"/>
/// marks the admin looking at the list (they can't take their own Admin role away).
/// Same shape as the API's <c>AdminUserDto</c>.
/// </summary>
public sealed record AdminUserDto(
    Guid Id,
    string FullName,
    string Email,
    string? PhoneNumber,
    Country Country,
    IReadOnlyCollection<string> Roles,
    DateTime MemberSince,
    string? ShopName,
    bool IsSuspended,
    DateTime? SuspendedUntil,
    string? SuspensionReason,
    bool CanBeSuspended,
    string? PhotoUrl,
    bool IsYou = false);
