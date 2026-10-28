namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>
/// On <c>GET /api/me</c>: where I stand with the Terms and the Privacy Policy. Same shape as the API's
/// <c>TermsStatusDto</c>. The app shows "We've updated our Terms" when <see cref="NeedsToAcceptTerms"/>, and a
/// one-time Privacy notice when <see cref="NeedsToSeePrivacy"/>.
/// </summary>
public sealed record TermsStatusDto(
    int CurrentTermsVersion,
    int? AcceptedTermsVersion,
    bool NeedsToAcceptTerms,
    int CurrentPrivacyVersion,
    bool NeedsToSeePrivacy);
