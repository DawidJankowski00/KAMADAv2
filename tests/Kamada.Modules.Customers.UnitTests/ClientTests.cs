using Kamada.Modules.Customers.Domain.Clients;
using Kamada.Shared.Kernel.Geography;

namespace Kamada.Modules.Customers.UnitTests;

public sealed class ClientTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly GeoPoint Bydgoszcz = new(53.1235, 18.0084);

    private static AddressDetails Details(
        string street = "Gdańska",
        string building = "10",
        string? apartment = "5",
        AddressType type = AddressType.Shipping) =>
        new(type, street, building, apartment, "Bydgoszcz", "85-005", "Polska");

    private static (Client Client, Address Address) ClientWithGeocodedAddress()
    {
        var client = Client.Create(" JKowalski ", " Jan.Kowalski@Example.com ", "Jan", "Kowalski", "+48 600 000 000");
        var address = client.AddAddress(Details());
        address.SetGeocodedLocation(Bydgoszcz, Now);
        return (client, address);
    }

    [Fact]
    public void Create_NormalizesUsernameAndEmail()
    {
        var (client, _) = ClientWithGeocodedAddress();

        client.Username.ShouldBe("jkowalski");
        client.Email.ShouldBe("jan.kowalski@example.com");
    }

    [Fact]
    public void UpdateAddress_DifferentBuilding_ClearsCoordinates()
    {
        var (client, address) = ClientWithGeocodedAddress();

        client.UpdateAddress(address.Id, Details(building: "12"), out var locationReset).IsSuccess.ShouldBeTrue();

        locationReset.ShouldBeTrue();
        address.Location.ShouldBeNull();
        address.CoordinatesSource.ShouldBeNull();
    }

    [Fact]
    public void UpdateAddress_OnlyApartmentOrTypeChanged_KeepsCoordinates()
    {
        var (client, address) = ClientWithGeocodedAddress();

        client.UpdateAddress(address.Id, Details(apartment: "7", type: AddressType.Billing), out var locationReset);

        locationReset.ShouldBeFalse();
        address.Location.ShouldBe(Bydgoszcz);
        address.ApartmentNumber.ShouldBe("7");
        address.Type.ShouldBe(AddressType.Billing);
    }

    [Fact]
    public void UpdateAddress_StreetCaseAndWhitespaceOnly_KeepsCoordinates()
    {
        var (client, address) = ClientWithGeocodedAddress();

        client.UpdateAddress(address.Id, Details(street: "  GDAŃSKA "), out var locationReset);

        locationReset.ShouldBeFalse();
        address.Location.ShouldNotBeNull();
    }

    [Fact]
    public void SetManualLocation_MarksSourceAsManual()
    {
        var (_, address) = ClientWithGeocodedAddress();
        var pin = new GeoPoint(53.13, 18.01);

        address.SetManualLocation(pin, Now.AddMinutes(5));

        address.Location.ShouldBe(pin);
        address.CoordinatesSource.ShouldBe(CoordinatesSource.Manual);
        address.GeocodedAt.ShouldBe(Now.AddMinutes(5));
    }

    [Fact]
    public void ArchiveAddress_HidesItFromActiveAddresses()
    {
        var (client, address) = ClientWithGeocodedAddress();

        client.ArchiveAddress(address.Id).IsSuccess.ShouldBeTrue();

        client.ActiveAddresses.ShouldBeEmpty();
        client.FindActiveAddress(address.Id).Error.ShouldBe(ClientErrors.AddressNotFound);
        client.ArchiveAddress(address.Id).Error.ShouldBe(ClientErrors.AddressNotFound);
    }
}
