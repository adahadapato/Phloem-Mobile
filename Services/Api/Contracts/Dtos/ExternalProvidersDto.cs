namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>
/// Answer of <c>GET /api/auth/external/providers</c>: which "Continue with …" buttons to show.
/// Same shape as the API's <c>ExternalProvidersResponse</c>.
/// </summary>
/// <param name="Google">True when "Continue with Google" works (the API has its Google keys).</param>
public sealed record ExternalProvidersDto(bool Google);
