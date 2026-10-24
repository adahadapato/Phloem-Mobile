namespace Phloem.Services.Api.Contracts.Commands;

/// <summary>Body of <c>POST /api/auth/external/exchange</c>: the one-time code from <c>phloem://auth?code=…</c>.</summary>
public sealed record ExchangeLoginCodeCommand(string Code);
