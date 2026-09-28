using Kamada.Shared.Contracts.Customers;

namespace Kamada.AdminPanel.Pages.Clients;

/// <summary>Mutable form model for an address.</summary>
public sealed class AddressModel
{
    public AddressType Type { get; set; } = AddressType.Shipping;

    public string Street { get; set; } = string.Empty;

    public string BuildingNumber { get; set; } = string.Empty;

    public string? ApartmentNumber { get; set; }

    public string City { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public string Country { get; set; } = "Polska";

    public bool IsEmpty => string.IsNullOrWhiteSpace(Street) && string.IsNullOrWhiteSpace(City);

    public static AddressModel From(AddressDto address) => new()
    {
        Type = address.Type,
        Street = address.Street,
        BuildingNumber = address.BuildingNumber,
        ApartmentNumber = address.ApartmentNumber,
        City = address.City,
        PostalCode = address.PostalCode,
        Country = address.Country,
    };

    public AddressRequest ToRequest() => new(Type, Street, BuildingNumber, ApartmentNumber, City, PostalCode, Country);
}
