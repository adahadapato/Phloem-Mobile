namespace Phloem.Services.Api.Contracts.Enums;

/// <summary>
/// Where a review's comment is. Same names as the API's <c>ReviewStatus</c>. The stars count in every status.
/// </summary>
public enum ReviewStatus
{
    /// <summary>The AI is looking at the comment (a moment).</summary>
    Checking = 1,
    /// <summary>On the site.</summary>
    Published = 2,
    /// <summary>The AI wasn't sure: a person looks first.</summary>
    WaitingForStaff = 3,
    /// <summary>Not shown on the site.</summary>
    Rejected = 4,
    /// <summary>Taken off the site by staff.</summary>
    Hidden = 5
}
