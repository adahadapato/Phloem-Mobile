namespace Phloem.Services.Api.Contracts.Enums;

/// <summary>What a Contact us message is about. Same names as the API's <c>ContactTopic</c>.</summary>
public enum ContactTopic
{
    /// <summary>A food order: delivery, refund, cancellation.</summary>
    Order = 1,

    /// <summary>A parcel: price, weighing, customs, tracking.</summary>
    Parcel = 2,

    /// <summary>Selling on Phloem.</summary>
    Selling = 3,

    /// <summary>My account.</summary>
    Account = 4,

    /// <summary>Anything else.</summary>
    Other = 5
}
