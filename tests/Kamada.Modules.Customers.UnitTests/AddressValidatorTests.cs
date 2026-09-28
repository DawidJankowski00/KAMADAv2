using Kamada.Modules.Customers.Application.Clients;
using Kamada.Shared.Contracts.Customers;

namespace Kamada.Modules.Customers.UnitTests;

public sealed class AddressValidatorTests
{
    private static AddressRequest Request(string postalCode, string country = "Polska") =>
        new(AddressType.Shipping, "Gdańska", "10", null, "Bydgoszcz", postalCode, country);

    [Theory]
    [InlineData("85-005", "Polska", true)]
    [InlineData("85005", "Polska", false)]
    [InlineData("85-00", "PL", false)]
    [InlineData("10115", "Germany", true)]
    public void PostalCode_PolishFormatEnforcedOnlyForPoland(string postalCode, string country, bool valid)
    {
        new AddressRequestValidator().Validate(Request(postalCode, country)).IsValid.ShouldBe(valid);
    }
}
