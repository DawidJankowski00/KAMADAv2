using Kamada.Shared.Contracts.Assistant;
using Kamada.Shared.Contracts.Customers;

namespace Kamada.Web.Admin.Pages.Clients;

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

    /// <summary>Everything the API requires is filled in (and a Polish postal code has the 00-000 format).</summary>
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(Street) && !string.IsNullOrWhiteSpace(BuildingNumber) && !string.IsNullOrWhiteSpace(City)
        && !string.IsNullOrWhiteSpace(PostalCode) && !string.IsNullOrWhiteSpace(Country) && PostalCodeError(PostalCode, Country) is null;

    /// <summary>Error for a filled-in postal code; empty is reported by the "required" check.</summary>
    public static string? PostalCodeError(string? postalCode, string? country)
    {
        if (string.IsNullOrWhiteSpace(postalCode))
        {
            return null;
        }

        var isPoland = country?.Trim() is "Polska" or "Poland" or "PL";
        return isPoland && !System.Text.RegularExpressions.Regex.IsMatch(postalCode.Trim(), "^[0-9]{2}-[0-9]{3}$")
            ? "Format kodu: 00-000"
            : null;
    }

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

    public static AddressModel From(NewAddressDto address) => new()
    {
        Street = address.Street ?? string.Empty,
        BuildingNumber = address.BuildingNumber ?? string.Empty,
        ApartmentNumber = address.ApartmentNumber,
        City = address.City ?? string.Empty,
        PostalCode = address.PostalCode ?? string.Empty,
        Country = address.Country,
    };

    public static AddressModel From(AddressRequest address) => new()
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
