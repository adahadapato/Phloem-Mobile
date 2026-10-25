using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>
/// One saved delivery address, as the API returns it. <see cref="RecipientName"/> and <see cref="PhoneNumber"/> are
/// null when the account holder's own name and phone apply.
/// </summary>
public sealed record AddressDto(Guid Id, string Label, string FullAddress, Country Country, bool IsDefault,
                                string? RecipientName = null, string? PhoneNumber = null);
