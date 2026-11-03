namespace Phloem.Services.Api.Contracts.Enums;

/// <summary>Whether a dish's wording passed Phloem's check. Same names as the API's <c>ContentReviewStatus</c>.
/// Buyers only see <see cref="Approved"/> dishes.</summary>
public enum ContentReviewStatus
{
    Approved = 1,
    Checking = 2,
    WithStaff = 3,
    Rejected = 4
}
