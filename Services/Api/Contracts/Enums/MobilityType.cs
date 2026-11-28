namespace Phloem.Services.Api.Contracts.Enums;

/// <summary>How a courier gets around. Same names as the API's <c>MobilityType</c>.</summary>
public enum MobilityType
{
    /// <summary>Walks (short food runs). No vehicle photos.</summary>
    OnFoot = 1,

    /// <summary>Pedal bicycle.</summary>
    Bicycle = 2,

    /// <summary>Electric bicycle.</summary>
    EBike = 3,

    /// <summary>Motorbike or scooter (okada).</summary>
    Motorbike = 4,

    /// <summary>Keke / tricycle.</summary>
    Keke = 5,

    /// <summary>Car.</summary>
    Car = 6,

    /// <summary>Van.</summary>
    Van = 7
}
