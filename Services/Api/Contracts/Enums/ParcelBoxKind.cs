namespace Phloem.Services.Api.Contracts.Enums;

/// <summary>Whose figures a box line holds. Same names as the API's <c>ParcelBoxKind</c>.</summary>
public enum ParcelBoxKind
{
    /// <summary>What the sender entered when booking.</summary>
    Declared = 1,

    /// <summary>What staff weighed and measured at the hub.</summary>
    Measured = 2
}
