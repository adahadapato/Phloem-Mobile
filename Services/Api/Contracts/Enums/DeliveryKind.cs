namespace Phloem.Services.Api.Contracts.Enums;

/// <summary>What a courier is delivering. Same names as the API's <c>DeliveryKind</c>.</summary>
public enum DeliveryKind
{
    /// <summary>A food order from a shop.</summary>
    Order = 1,

    /// <summary>A parcel from another country.</summary>
    Parcel = 2
}
