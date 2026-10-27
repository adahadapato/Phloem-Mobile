using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>
/// A country's commission (admin). <see cref="IsSet"/> is false while no rate has been chosen (it counts as 0%).
/// Same shape as the API's <c>CommissionDto</c>.
/// </summary>
public sealed record CommissionDto(Country Country, decimal RatePercent, bool IsSet);
