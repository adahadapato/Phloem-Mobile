namespace Phloem.Services.Api.Contracts.Enums;

/// <summary>Where a courier's vehicle details are. Same names as the API's <c>VehicleCheckStatus</c>.</summary>
public enum VehicleCheckStatus
{
    WaitingForStaff = 1,
    Approved = 2,
    Rejected = 3,
    Replaced = 4
}
