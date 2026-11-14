using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>My review of an order or parcel. Same as the API's <c>MyReviewDto</c>.</summary>
public sealed record MyReviewDto(int Stars, string? Comment, bool ShowPublicly, ReviewStatus Status, string DisplayName,
                                 string PlaceText, DateTimeOffset WrittenAt);

/// <summary>
/// The "Rate it" card: can I review it, why not, my review if written, and how it would show on the site
/// ("Chioma A. · Order from Mama's Kitchen"). Same as the API's <c>ReviewStateDto</c>.
/// </summary>
public sealed record ReviewStateDto(bool CanReview, string? Reason, MyReviewDto? Review, string ShowAs);
