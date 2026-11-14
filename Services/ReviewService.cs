using Phloem.Services.Api;
using Phloem.Services.Api.Contracts.Commands;
using Phloem.Services.Api.Contracts.Dtos;
using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services;

/// <summary>
/// Reviews after delivery on the API (<c>/api/orders/{n}/review</c>, <c>/api/shipments/{t}/review</c>). Stars count
/// straight away; a comment goes on the site when the AI rates it clean, otherwise after a person has looked.
/// </summary>
public sealed class ReviewService(ApiClient api)
{
    private static string PathFor(ReviewKind kind, string reference) => kind == ReviewKind.Order
        ? $"api/orders/{Uri.EscapeDataString(reference)}/review"
        : $"api/shipments/{Uri.EscapeDataString(reference)}/review";

    /// <summary>Can I rate it, and my review if I have.</summary>
    public Task<ApiResult<ReviewStateDto>> GetStateAsync(ReviewKind kind, string reference, CancellationToken ct = default) =>
        api.GetAsync<ReviewStateDto>(PathFor(kind, reference), ct);

    /// <summary>Rates a delivered order or parcel (once, within 60 days).</summary>
    public Task<ApiResult<MyReviewDto>> SubmitAsync(ReviewKind kind, string reference, RateRequest request,
                                                   CancellationToken ct = default) =>
        api.PostAsync<MyReviewDto>(PathFor(kind, reference), request, ct);
}
