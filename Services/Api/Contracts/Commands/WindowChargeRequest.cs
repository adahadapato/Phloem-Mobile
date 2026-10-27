namespace Phloem.Services.Api.Contracts.Commands;

/// <summary>Body of "set a delivery time's charge": what it adds to an order (0 = free). Same as the API's <c>WindowChargeRequest</c>.</summary>
public sealed record WindowChargeRequest(decimal ExtraCharge);
