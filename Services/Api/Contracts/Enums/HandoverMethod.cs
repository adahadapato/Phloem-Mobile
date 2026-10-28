namespace Phloem.Services.Api.Contracts.Enums;

/// <summary>How a delivery was handed over. Same names as the API's <c>HandoverMethod</c>.</summary>
public enum HandoverMethod
{
    /// <summary>Handed to the person.</summary>
    InPerson = 1,

    /// <summary>Left at the front door.</summary>
    FrontDoor = 2,

    /// <summary>Left with a neighbour (say which one).</summary>
    Neighbour = 3,

    /// <summary>Left in a safe place (porch, behind the gate).</summary>
    SafePlace = 4,

    /// <summary>Left at reception, a concierge or the security gate.</summary>
    Reception = 5
}
