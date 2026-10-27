using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Commands;

/// <summary>
/// Body of <c>POST /api/contact/messages</c> (the Contact us form). Same as the API's
/// <c>SendContactMessageCommand</c>; <c>Website</c> (the website's spam trap) is always empty from the app.
/// </summary>
public sealed record SendContactMessageCommand(
    string Name,
    string Email,
    ContactTopic Topic,
    string? Reference,
    string Message,
    string? Website = null);
