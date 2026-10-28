namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>One entry in "Choose a courier". <see cref="IsMe"/> = the seller's "I'll deliver it myself".</summary>
public sealed record CourierOptionDto(Guid Id, string Name, bool IsMe);
