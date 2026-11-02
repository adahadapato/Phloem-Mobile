using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>
/// One person on Admin → Staff: an owner (Admin) or someone with a staff job role. Same shape as the API's
/// <c>StaffMemberDto</c> (GET /api/admin/staff).
/// </summary>
/// <param name="Id">The person.</param>
/// <param name="FullName">"Ama Mensah".</param>
/// <param name="Email">Their login email.</param>
/// <param name="HomeCountry">The country on their profile.</param>
/// <param name="IsAdmin">The owners: everything, everywhere.</param>
/// <param name="JobRoles">Stored names, e.g. "ShipmentManager".</param>
/// <param name="JobRoleNames">As people read them, e.g. "Parcel hub".</param>
/// <param name="CoversAllCountries">Owners, and staff given every country.</param>
/// <param name="Countries">The countries they cover; empty when <paramref name="CoversAllCountries"/>.</param>
/// <param name="TwoFactorEnabled">Two-step sign-in on (required for all staff).</param>
/// <param name="LastSignIn">When they last signed in (UTC); null = never.</param>
/// <param name="IsSuspended">True while suspended.</param>
/// <param name="IsYou">The person looking.</param>
public sealed record StaffMemberDto(
    Guid Id,
    string FullName,
    string Email,
    Country HomeCountry,
    bool IsAdmin,
    IReadOnlyList<string> JobRoles,
    IReadOnlyList<string> JobRoleNames,
    bool CoversAllCountries,
    IReadOnlyList<Country> Countries,
    bool TwoFactorEnabled,
    DateTime? LastSignIn,
    bool IsSuspended,
    bool IsYou);
