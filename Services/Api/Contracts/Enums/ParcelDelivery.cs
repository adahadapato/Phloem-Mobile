namespace Phloem.Services.Api.Contracts.Enums;

/// <summary>How the parcel reaches the recipient. Same names as the API's <c>ParcelDelivery</c>.</summary>
public enum ParcelDelivery
{
    /// <summary>A courier brings it to the door (the zone's charge).</summary>
    ToDoor = 1,

    /// <summary>The recipient collects it from our hub (no delivery charge).</summary>
    CollectFromHub = 2
}
