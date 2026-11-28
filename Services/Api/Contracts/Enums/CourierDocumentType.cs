namespace Phloem.Services.Api.Contracts.Enums;

/// <summary>A courier's registration documents. Same names as the API's <c>CourierDocumentType</c>.</summary>
public enum CourierDocumentType
{
    /// <summary>Passport-style photo of the courier's face.</summary>
    PassportPhoto = 1,

    /// <summary>NIN (Nigeria), Ghana Card (Ghana), passport or photo ID card (UK).</summary>
    NationalId = 2,

    /// <summary>Driving licence (motorbike, keke, car, van).</summary>
    DrivingLicence = 3
}
