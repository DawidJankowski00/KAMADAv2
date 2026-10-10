namespace Kamada.Modules.Customers.Contracts;

/// <summary>What was said about a client (any part may be missing). Names are expected in the nominative form.</summary>
public sealed record ClientMention(string? Name, string? Surname, string? PhoneNumber, string? Email)
{
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Surname)
        && string.IsNullOrWhiteSpace(PhoneNumber) && string.IsNullOrWhiteSpace(Email);
}

/// <summary>Why a client was proposed, strongest first.</summary>
public enum ClientMatchReason
{
    Phone,
    Email,
    Name,
}

/// <summary>An active client similar to a <see cref="ClientMention"/>; <see cref="Score"/> is 0 … 1 (1 = exact phone/e-mail).</summary>
public sealed record ClientCandidate(
    Guid Id,
    string Name,
    string Surname,
    string Email,
    string PhoneNumber,
    double Score,
    ClientMatchReason Reason);

/// <summary>What was said about an address (any part may be missing).</summary>
public sealed record AddressMention(string? Street, string? BuildingNumber, string? ApartmentNumber, string? City, string? PostalCode)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Street) && string.IsNullOrWhiteSpace(City);
}

/// <summary>An existing active shipping address that matches an <see cref="AddressMention"/>.</summary>
public sealed record ShippingAddressMatch(
    Guid AddressId,
    string Street,
    string BuildingNumber,
    string? ApartmentNumber,
    string City,
    string PostalCode,
    double? Latitude,
    double? Longitude);
