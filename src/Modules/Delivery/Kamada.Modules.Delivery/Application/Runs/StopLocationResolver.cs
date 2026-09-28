using Kamada.Modules.Customers.Contracts;
using Kamada.Modules.Ordering.Contracts;
using Kamada.Shared.Kernel.Geography;

namespace Kamada.Modules.Delivery.Application.Runs;

/// <summary>
/// Where to drive for each order: the address's <em>current</em> coordinates (an admin may have pinned it after
/// the order was placed), falling back to the snapshot stored in the order.
/// </summary>
internal sealed class StopLocationResolver(ICustomerDirectory customers)
{
    public async Task<IReadOnlyDictionary<Guid, GeoPoint?>> ResolveAsync(IReadOnlyCollection<DeliveryOrder> orders, CancellationToken cancellationToken)
    {
        if (orders.Count == 0)
        {
            return new Dictionary<Guid, GeoPoint?>();
        }

        var current = await customers.GetAddressLocationsAsync([.. orders.Select(o => o.AddressId)], cancellationToken);
        return orders.ToDictionary(
            o => o.OrderId,
            o => current.TryGetValue(o.AddressId, out var address) && ToPoint(address.Latitude, address.Longitude) is { } point
                ? point
                : ToPoint(o.Latitude, o.Longitude));
    }

    private static GeoPoint? ToPoint(double? latitude, double? longitude) =>
        latitude is { } lat && longitude is { } lng && GeoPoint.IsValid(lat, lng) ? new GeoPoint(lat, lng) : null;
}
