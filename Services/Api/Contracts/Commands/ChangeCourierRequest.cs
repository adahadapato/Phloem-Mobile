namespace Phloem.Services.Api.Contracts.Commands;

/// <summary>Body of the parcel staff's "change courier".</summary>
public sealed record ChangeCourierRequest(Guid CourierId);
