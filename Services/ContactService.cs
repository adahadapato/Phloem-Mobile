using Phloem.Services.Api;
using Phloem.Services.Api.Contracts.Commands;
using Phloem.Services.Api.Contracts.Dtos;

namespace Phloem.Services;

/// <summary>
/// Contact us on the API, the same as the website's: <c>GET /api/contact</c> (the details) and
/// <c>POST /api/contact/messages</c> (the form; signed in or not, saved for the team, emailed to them, and a
/// confirmation emailed to the sender).
/// </summary>
public sealed class ContactService(ApiClient api)
{
    /// <summary>The support email, phone, WhatsApp, address, hours and reply time.</summary>
    public Task<ApiResult<ContactDetailsDto>> GetDetailsAsync(CancellationToken ct = default) =>
        api.GetAsync<ContactDetailsDto>("api/contact", ct);

    /// <summary>Sends the form. A 429 means too many messages in a short time (the API's message says so).</summary>
    public Task<ApiResult> SendAsync(SendContactMessageCommand command, CancellationToken ct = default) =>
        api.PostAsync("api/contact/messages", command, ct);
}
