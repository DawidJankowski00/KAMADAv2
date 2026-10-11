namespace Kamada.Shared.Matching.Addresses;

/// <summary>What was said about an address (any part may be missing).</summary>
public sealed record AddressMention(string? Street, string? BuildingNumber, string? ApartmentNumber, string? City, string? PostalCode)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Street) && string.IsNullOrWhiteSpace(City);
}
