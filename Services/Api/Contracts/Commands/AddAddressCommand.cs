using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Commands;

/// <summary>Body of <c>POST /api/me/addresses</c>. Same shape as the API's <c>AddAddressCommand</c>.
/// <c>RecipientName</c> / <c>PhoneNumber</c>: who receives it and who the seller calls; null = me.</summary>
public sealed record AddAddressCommand(string Label, string FullAddress, Country Country, bool MakeDefault,
                                       string? RecipientName = null, string? PhoneNumber = null);
