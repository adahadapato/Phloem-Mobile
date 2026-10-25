using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Commands;

/// <summary>Body of "add" and "change" a line of the "what can be sent" list. Same as the API's
/// <c>ParcelItemRuleRequest</c>.</summary>
public sealed record ParcelItemRuleRequest(ParcelItemRuleKind Kind, string Name, string? Detail);
