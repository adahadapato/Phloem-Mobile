namespace Phloem.Models;

public class DeliveryAddress
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Label { get; set; } = "Home"; // e.g. Home, Work, Family House
    public string FullAddress { get; set; } = string.Empty;
    public string Country { get; set; } = "Ghana";
    public bool IsDefault { get; set; }

    /// <summary>Who receives the food; null = the account holder.</summary>
    public string? RecipientName { get; set; }

    /// <summary>Who the seller calls; null = the account holder's phone.</summary>
    public string? PhoneNumber { get; set; }
}