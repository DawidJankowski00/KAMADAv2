using Kamada.Shared.Kernel.Domain;
using Kamada.Shared.Kernel.Geography;

namespace Kamada.Modules.Customers.Domain.Clients;

/// <summary>
/// Client address with optional coordinates. Addresses are archived rather than deleted because orders
/// keep referencing them. An address without coordinates cannot be used for route optimization.
/// </summary>
internal sealed class Address : Entity, IAuditable
{
    private Address(Guid id, Guid clientId)
        : base(id) => ClientId = clientId;

    public Guid ClientId { get; private set; }

    public AddressType Type { get; private set; }

    public string Street { get; private set; } = string.Empty;

    public string BuildingNumber { get; private set; } = string.Empty;

    public string? ApartmentNumber { get; private set; }

    public string City { get; private set; } = string.Empty;

    public string PostalCode { get; private set; } = string.Empty;

    public string Country { get; private set; } = string.Empty;

    public double? Latitude { get; private set; }

    public double? Longitude { get; private set; }

    public CoordinatesSource? CoordinatesSource { get; private set; }

    /// <summary>When the coordinates were set (geocoded or pinned manually).</summary>
    public DateTimeOffset? GeocodedAt { get; private set; }

    public bool IsArchived { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public GeoPoint? Location => Latitude is { } lat && Longitude is { } lng ? new GeoPoint(lat, lng) : null;

    public AddressDetails Details => new(Type, Street, BuildingNumber, ApartmentNumber, City, PostalCode, Country);

    internal static Address Create(Guid clientId, AddressDetails details)
    {
        var address = new Address(Guid.CreateVersion7(), clientId);
        address.Apply(details);
        return address;
    }

    /// <summary>Updates the address; returns true when the building changed and the location must be resolved again.</summary>
    internal bool Update(AddressDetails details)
    {
        var buildingChanged = !Details.IsSameBuildingAs(details);
        Apply(details);
        if (buildingChanged)
        {
            ClearLocation();
        }

        return buildingChanged;
    }

    public void SetGeocodedLocation(GeoPoint location, DateTimeOffset now) => SetLocation(location, Clients.CoordinatesSource.Geocoded, now);

    public void SetManualLocation(GeoPoint location, DateTimeOffset now) => SetLocation(location, Clients.CoordinatesSource.Manual, now);

    internal void Archive() => IsArchived = true;

    private void SetLocation(GeoPoint location, CoordinatesSource source, DateTimeOffset now)
    {
        Latitude = location.Latitude;
        Longitude = location.Longitude;
        CoordinatesSource = source;
        GeocodedAt = now;
    }

    private void ClearLocation()
    {
        Latitude = null;
        Longitude = null;
        CoordinatesSource = null;
        GeocodedAt = null;
    }

    private void Apply(AddressDetails details)
    {
        Type = details.Type;
        Street = details.Street;
        BuildingNumber = details.BuildingNumber;
        ApartmentNumber = details.ApartmentNumber;
        City = details.City;
        PostalCode = details.PostalCode;
        Country = details.Country;
    }
}
