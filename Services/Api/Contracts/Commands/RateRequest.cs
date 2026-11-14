namespace Phloem.Services.Api.Contracts.Commands;

/// <summary>
/// Body of "rate a delivered order / parcel": 1–5 stars, an optional comment (≤ 500), and whether the comment may be
/// shown on the site. Same as the API's <c>ReviewEndpoints.ReviewRequest</c> (named differently here because
/// <see cref="ReviewRequest"/> is the document review).
/// </summary>
public sealed record RateRequest(int Stars, string? Comment, bool ShowPublicly);
