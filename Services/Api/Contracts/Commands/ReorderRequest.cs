namespace Phloem.Services.Api.Contracts.Commands;

/// <summary>Body of "put the list in a new order": every line's id, first to last. Same as the API's
/// <c>ReorderRequest</c>.</summary>
public sealed record ReorderRequest(IReadOnlyList<Guid> Ids);
