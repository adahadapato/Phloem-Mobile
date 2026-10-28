using Phloem.Services.Api.Contracts.Enums;

namespace Phloem.Services.Api.Contracts.Dtos;

/// <summary>Answer of <c>GET /api/me</c>: the signed-in user. Same shape as the API's <c>ProfileDto</c>.</summary>
public sealed record ProfileDto(
    Guid Id,
    string FirstName,
    string LastName,
    string FullName,
    string Email,
    bool EmailConfirmed,
    string? PhoneNumber,
    Country Country,
    Currency Currency,
    string? PhotoUrl,
    DateTime MemberSince,
    bool HasShop,
    DateOnly? DateOfBirth,
    HomeAddressDto? HomeAddress,
    bool HasPersonalDetails,
    bool NotifyOrderUpdates,
    bool NotifyShipmentUpdates,
    bool NotifyOffers,
    bool NotifyNewDishes,
    bool NotifyShopOrders,
    IReadOnlyList<AddressDto> Addresses,
    TermsStatusDto? Terms = null);
