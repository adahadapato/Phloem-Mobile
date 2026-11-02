using Phloem.Services.Api.Contracts.Commands;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>
/// Answer of login, register, two-factor and refresh. Same shape as the API's <c>AuthTokensDto</c>.
/// </summary>
/// <remarks>When <see cref="RequiresTwoFactor"/> is true there are no tokens yet: ask for the 6-digit code and send it
/// with <see cref="TwoFactorChallengeToken"/> (<see cref="CompleteTwoFactorLoginCommand"/>).
/// <para><see cref="Permissions"/> and <see cref="Countries"/> (staff only): what the BACKEND decided this person may
/// do and where ("*" = everything / every country). The app uses them only to show or hide menus; the API checks
/// every call itself.</para></remarks>
public sealed record AuthTokensDto(
    string? AccessToken,
    string? RefreshToken,
    DateTime? ExpiresAt,
    DateTime? RefreshTokenExpiresAt,
    Guid? UserId,
    IReadOnlyCollection<string>? Roles,
    string? FullName,
    string? Email,
    bool RequiresTwoFactor = false,
    string? TwoFactorChallengeToken = null,
    IReadOnlyCollection<string>? Permissions = null,
    IReadOnlyCollection<string>? Countries = null);
