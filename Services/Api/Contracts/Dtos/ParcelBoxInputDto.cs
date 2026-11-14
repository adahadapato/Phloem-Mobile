namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>
/// One box line: weight of ONE box, its outside size in cm (all three sides or none; required at the hub) and how many
/// identical boxes. Same shape as the API's <c>ParcelBoxInputDto</c>.
/// </summary>
public sealed record ParcelBoxInputDto(decimal WeightKg, decimal? LengthCm, decimal? WidthCm, decimal? HeightCm,
                                       int Quantity = 1);
