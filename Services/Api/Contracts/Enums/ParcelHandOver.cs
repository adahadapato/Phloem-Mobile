namespace Phloem.Services.Api.Contracts.Enums;

/// <summary>How the parcel gets to us in the sending country. Same names as the API's <c>ParcelHandOver</c>.</summary>
public enum ParcelHandOver
{
    /// <summary>The sender brings it to the drop-off hub.</summary>
    HubDropOff = 1,

    /// <summary>We collect it from the sender (extra charge).</summary>
    HomePickup = 2
}
