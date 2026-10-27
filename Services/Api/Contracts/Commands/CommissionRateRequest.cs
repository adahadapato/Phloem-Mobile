namespace Phloem.Services.Api.Contracts.Commands;

/// <summary>Body of "set a commission" (the country is in the URL), e.g. 10 = 10% of the dishes. Same as the API's <c>CommissionRateRequest</c>.</summary>
public sealed record CommissionRateRequest(decimal RatePercent);
