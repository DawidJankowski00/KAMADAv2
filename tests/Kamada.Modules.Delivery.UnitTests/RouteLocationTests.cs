using Kamada.Modules.Delivery.Domain.Locations;
using Kamada.Shared.Kernel.Geography;

namespace Kamada.Modules.Delivery.UnitTests;

public sealed class RouteLocationTests
{
    private static RouteLocationDetails Details(string name = "Magazyn", string building = "5") =>
        new($" {name} ", "Fordońska", building, "Bydgoszcz", "85-719", "Polska");

    [Fact]
    public void Create_IsActiveWithoutCoordinates()
    {
        var location = RouteLocation.Create(Details());

        location.IsActive.ShouldBeTrue();
        location.Name.ShouldBe("Magazyn");
        location.Location.ShouldBeNull();
    }

    [Fact]
    public void Update_OnlyNameChanged_KeepsCoordinates()
    {
        var location = RouteLocation.Create(Details());
        location.SetLocation(new GeoPoint(53.12, 18.05));

        location.Update(Details(name: "Magazyn główny")).ShouldBeFalse();

        location.Location.ShouldBe(new GeoPoint(53.12, 18.05));
    }

    [Fact]
    public void Update_BuildingChanged_ClearsCoordinates()
    {
        var location = RouteLocation.Create(Details());
        location.SetLocation(new GeoPoint(53.12, 18.05));

        location.Update(Details(building: "7")).ShouldBeTrue();

        location.Location.ShouldBeNull();
    }
}
