using Kamada.Shared.Kernel.Geography;

namespace Kamada.Modules.RouteOptimization.Contracts;

/// <summary>Builds navigation links for couriers (official Google Maps URLs, no API key, no scraping).</summary>
public interface INavigationLinkBuilder
{
    /// <param name="points">The whole route in driving order: start, stops, end (at least two points).</param>
    IReadOnlyList<NavigationLink> BuildRouteLinks(IReadOnlyList<GeoPoint> points);

    /// <summary>Directions from the device's current location to one point.</summary>
    string BuildDestinationLink(GeoPoint destination);
}
