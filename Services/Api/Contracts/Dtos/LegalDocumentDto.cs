using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>
/// <c>GET /api/legal/{type}</c>: the current Terms or Privacy Policy. Same shape as the API's <c>LegalDocumentDto</c>.
/// <see cref="Content"/> is Markdown; headings may end with an anchor like {#sellers}.
/// </summary>
public sealed record LegalDocumentDto(
    LegalDocumentType Type,
    int Version,
    string Title,
    string Content,
    bool IsDraft,
    DateTime PublishedAt);
