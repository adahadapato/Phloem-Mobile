using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>
/// <c>GET /api/parcel-rules</c>: one line of the "what can be sent" list (the list comes in display order).
/// Same shape as the API's <c>ParcelItemRuleDto</c>.
/// </summary>
public sealed record ParcelItemRuleDto(
    Guid Id,
    ParcelItemRuleKind Kind,
    string Name,
    string? Detail,
    int SortOrder);
