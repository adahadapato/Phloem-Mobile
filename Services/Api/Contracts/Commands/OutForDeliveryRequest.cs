namespace Phloem.Services.Api.Contracts.Commands;

/// <summary>Body of the parcel staff's "out for delivery": the courier and an optional timeline note.</summary>
public sealed record OutForDeliveryRequest(Guid CourierId, string? Note);
