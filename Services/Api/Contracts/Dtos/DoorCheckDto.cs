using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>Door safety while a delivery is on its way: the door word to ask for, the handover PIN, and who is
/// coming. Same shape as the API's <c>DoorCheckDto</c>.</summary>
public sealed record DoorCheckDto(string DoorWord, string HandoverPin, CourierCardDto? Courier, string EmergencyNumber);

/// <summary>Who is coming. Same shape as the API's <c>CourierCardDto</c>; the photo link works for about 10 minutes.</summary>
public sealed record CourierCardDto(string FirstName, string? PhotoUrl, string? MobilityName, string? Colour,
                                    string? Make, string? RegistrationNumber);

/// <summary>What to do after "This isn't my courier". Same shape as the API's <c>CourierConcernRaisedDto</c>.</summary>
public sealed record CourierConcernRaisedDto(string Message, string EmergencyNumber);

/// <summary>"This isn't my courier" (POST /api/couriers/concerns). Same shape as the API's command.</summary>
public sealed record RaiseCourierConcernCommand(CourierConcernKind Kind, string Reference, string? Note);
