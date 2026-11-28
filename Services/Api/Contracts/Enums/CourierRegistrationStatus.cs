namespace Phloem.Services.Api.Contracts.Enums;

/// <summary>Where a courier registration is. Same names as the API's <c>CourierRegistrationStatus</c>.</summary>
public enum CourierRegistrationStatus
{
    /// <summary>Started (or not started): adding vehicle and documents.</summary>
    Draft = 1,

    /// <summary>Sent; Phloem staff are checking it.</summary>
    WaitingForStaff = 2,

    /// <summary>An approved courier.</summary>
    Approved = 3,

    /// <summary>Not approved yet: fix what's marked and send again.</summary>
    Rejected = 4
}
