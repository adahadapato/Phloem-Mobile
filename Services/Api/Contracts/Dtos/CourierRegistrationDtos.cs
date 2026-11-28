using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>"Become a courier": vehicle, documents, what's still needed, where it stands. Same shape as the API's
/// <c>MyCourierRegistrationDto</c>.</summary>
public sealed record MyCourierRegistrationDto(
    CourierRegistrationStatus Status,
    string Message,
    Country Country,
    MyVehicleDto? Vehicle,
    IReadOnlyList<MyCourierDocumentDto> Documents,
    IReadOnlyList<string> StillNeeded,
    bool CanSubmit,
    string? DecisionNote,
    DateTime? SubmittedAtUtc);

/// <summary>One document on "Become a courier". Same shape as the API's <c>MyCourierDocumentDto</c>.
/// <see cref="FileUrl"/> works for about 10 minutes.</summary>
public sealed record MyCourierDocumentDto(
    CourierDocumentType Type,
    string Name,
    bool NeedsNumber,
    string NumberHelp,
    bool Given,
    string? Number,
    DocumentStatus? Status,
    string? RejectionReason,
    string? FileUrl);

/// <summary>The courier's own vehicle details, with hints they can fix. Same shape as the API's <c>MyVehicleDto</c>.
/// Photo links work for about 15 minutes.</summary>
public sealed record MyVehicleDto(
    Guid Id,
    MobilityType Mobility,
    string MobilityName,
    string? RegistrationNumber,
    string? Make,
    string? Colour,
    string? FrontPhotoUrl,
    string? SidePhotoUrl,
    VehicleCheckStatus Status,
    string Message,
    IReadOnlyList<string> Hints,
    string? StaffNote,
    DateTime SubmittedAtUtc);
