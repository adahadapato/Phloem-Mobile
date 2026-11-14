using Phloem.Services.Api.Contracts.Dtos;

namespace Phloem.Services.Api.Contracts.Commands;

/// <summary>
/// Body of "received at the hub": every box weighed and measured. (<see cref="WeighedWeightKg"/> is only for older
/// apps; this app sends boxes.)
/// </summary>
public sealed record ReceiveRequest(decimal? WeighedWeightKg, IReadOnlyList<ParcelBoxInputDto>? Boxes);
