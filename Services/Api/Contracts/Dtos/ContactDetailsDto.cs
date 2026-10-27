namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>
/// <c>GET /api/contact</c>: the support email, phone, WhatsApp, address, opening hours and reply time.
/// Null = not shown. Same shape as the API's <c>ContactDetailsDto</c>.
/// </summary>
public sealed record ContactDetailsDto(
    string Email,
    string? Phone,
    string? WhatsApp,
    string? Address,
    string? OpeningHours,
    string? ReplyTime);
