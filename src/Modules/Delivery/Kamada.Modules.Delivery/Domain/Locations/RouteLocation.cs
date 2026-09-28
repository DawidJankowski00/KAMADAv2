using Kamada.Shared.Kernel.Domain;
using Kamada.Shared.Kernel.Geography;

namespace Kamada.Modules.Delivery.Domain.Locations;

/// <summary>
/// Start or end point of delivery runs (warehouse, courier base). Deactivated rather than deleted because
/// runs keep referencing it. A location without coordinates cannot be used for route optimization.
/// </summary>
internal sealed class RouteLocation : Entity, IAuditable
{
    private RouteLocation(Guid id)
        : base(id)
    {
    }

    public string Name { get; private set; } = string.Empty;

    public string Street { get; private set; } = string.Empty;

    public string BuildingNumber { get; private set; } = string.Empty;

    public string City { get; private set; } = string.Empty;

    public string PostalCode { get; private set; } = string.Empty;

    public string Country { get; private set; } = string.Empty;

    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public GeoPoint? Location => Latitude is { } lat && Longitude is { } lng ? new GeoPoint(lat, lng) : null;

    public RouteLocationDetails Details => new(Name, Street, BuildingNumber, City, PostalCode, Country);

    public static RouteLocation Create(RouteLocationDetails details)
    {
        var location = new RouteLocation(Guid.CreateVersion7()) { IsActive = true };
        location.Apply(details);
        return location;
    }

    /// <summary>Updates the location; returns true when the building changed and the coordinates were cleared.</summary>
    public bool Update(RouteLocationDetails details)
    {
        var buildingChanged = !Details.IsSameBuildingAs(details);
        Apply(details);
        if (buildingChanged)
        {
            Latitude = null;
            Longitude = null;
        }

        return buildingChanged;
    }

    /// <summary>Geocoded or pinned manually on the map.</summary>
    public void SetLocation(GeoPoint location)
    {
        Latitude = location.Latitude;
        Longitude = location.Longitude;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private void Apply(RouteLocationDetails details)
    {
        Name = details.Name;
        Street = details.Street;
        BuildingNumber = details.BuildingNumber;
        City = details.City;
        PostalCode = details.PostalCode;
        Country = details.Country;
    }
}
