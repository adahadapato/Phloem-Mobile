namespace Phloem.Services.Api.Contracts.Commands;

/// <summary>Body of the seller's "send out" / "change courier"; null = "I'll deliver it myself".</summary>
public sealed record CourierRequest(Guid? CourierId);
