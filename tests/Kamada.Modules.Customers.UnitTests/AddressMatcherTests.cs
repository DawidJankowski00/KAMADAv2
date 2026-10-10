using Kamada.Modules.Customers.Application.Lookup;
using Kamada.Modules.Customers.Contracts;

namespace Kamada.Modules.Customers.UnitTests;

public sealed class AddressMatcherTests
{
    private static readonly AddressRow Grunwaldzka = new(Guid.NewGuid(), "Aleja Grunwaldzka", "15", "4", "Gdańsk", "80-236", 54.38, 18.6);
    private static readonly AddressRow Waly = new(Guid.NewGuid(), "ul. Wały Chrobrego", "12A", null, "Szczecin", "70-500", null, null);

    private static AddressMention Mention(string? street, string? building, string? apartment = null, string? city = null) =>
        new(street, building, apartment, city, null);

    [Fact]
    public void FindSame_SameStreetBuildingAndApartment_ReturnsAddress() =>
        AddressMatcher.FindSame(Mention("grunwaldzka", "15", "4", "Gdansk"), [Grunwaldzka, Waly]).ShouldBe(Grunwaldzka);

    [Fact]
    public void FindSame_BuildingNumberWithSpaceAndPrefixedStreet_ReturnsAddress() =>
        AddressMatcher.FindSame(Mention("Wały Chrobrego", "12 a"), [Grunwaldzka, Waly]).ShouldBe(Waly);

    [Fact]
    public void FindSame_DifferentApartment_ReturnsNull() =>
        AddressMatcher.FindSame(Mention("Grunwaldzka", "15", "7"), [Grunwaldzka]).ShouldBeNull();

    [Fact]
    public void FindSame_ApartmentNotSaidAndOnlyOneAddressInBuilding_ReturnsIt() =>
        AddressMatcher.FindSame(Mention("Grunwaldzka", "15"), [Grunwaldzka]).ShouldBe(Grunwaldzka);

    [Fact]
    public void FindSame_ApartmentNotSaidAndSeveralInBuilding_ReturnsNull()
    {
        var other = Grunwaldzka with { Id = Guid.NewGuid(), ApartmentNumber = "9" };

        AddressMatcher.FindSame(Mention("Grunwaldzka", "15"), [Grunwaldzka, other]).ShouldBeNull();
    }

    [Fact]
    public void FindSame_MisheardCityButSameStreetAndBuilding_StillMatches() =>
        AddressMatcher.FindSame(Mention("Grunwaldzka", "15", "4", "Gdynia"), [Grunwaldzka]).ShouldBe(Grunwaldzka);

    [Fact]
    public void FindSame_SameStreetInTwoCities_CityDecides()
    {
        var szczecin = Grunwaldzka with { Id = Guid.NewGuid(), City = "Szczecin" };

        AddressMatcher.FindSame(Mention("Grunwaldzka", "15", "4", "Szczecin"), [Grunwaldzka, szczecin]).ShouldBe(szczecin);
    }

    [Fact]
    public void FindSame_MissingBuilding_ReturnsNull() =>
        AddressMatcher.FindSame(Mention("Grunwaldzka", null), [Grunwaldzka]).ShouldBeNull();

    [Theory]
    [InlineData("ul. Długa", "dluga")]
    [InlineData("Aleja Grunwaldzka", "grunwaldzka")]
    [InlineData("Plac", "plac")]
    public void StreetName_DropsTypePrefix(string street, string expected) =>
        AddressMatcher.StreetName(street).ShouldBe(expected);
}
