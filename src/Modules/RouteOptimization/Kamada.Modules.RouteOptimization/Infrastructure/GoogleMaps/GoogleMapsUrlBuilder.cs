using Kamada.Modules.RouteOptimization.Contracts;
using Kamada.Shared.Kernel.Geography;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.RouteOptimization.Infrastructure.GoogleMaps;

/// <summary>
/// Official Google Maps URLs (<c>https://www.google.com/maps/dir/?api=1…</c>, no API key) with coordinates instead
/// of text addresses. A route with more waypoints than one link allows is split into chained, evenly sized parts:
/// the destination of part k is the origin of part k + 1.
/// </summary>
internal sealed class GoogleMapsUrlBuilder(IOptions<GoogleMapsOptions> options) : INavigationLinkBuilder
{
    private const string BaseUrl = "https://www.google.com/maps/dir/?api=1";

    public IReadOnlyList<NavigationLink> BuildRouteLinks(IReadOnlyList<GeoPoint> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 2)
        {
            throw new ArgumentException("A route needs at least an origin and a destination.", nameof(points));
        }

        var legs = points.Count - 1;
        var legsPerLink = options.Value.MaxWaypointsPerLink + 1;
        var parts = (legs + legsPerLink - 1) / legsPerLink;
        var legsPerPart = (legs + parts - 1) / parts; // spread evenly instead of 10 + 10 + 1

        var links = new List<NavigationLink>(parts);
        for (var part = 0; part < parts; part++)
        {
            var from = part * legsPerPart;
            var to = Math.Min(from + legsPerPart, legs);
            var url = Directions(points[from], points[to], [.. points.Skip(from + 1).Take(to - from - 1)]);
            links.Add(new NavigationLink(part + 1, parts, from, to, url));
        }

        return links;
    }

    public string BuildDestinationLink(GeoPoint destination) =>
        $"{BaseUrl}&destination={Encode(destination)}&travelmode={options.Value.TravelMode}&dir_action=navigate";

    private string Directions(GeoPoint origin, GeoPoint destination, GeoPoint[] waypoints)
    {
        var url = $"{BaseUrl}&origin={Encode(origin)}&destination={Encode(destination)}";
        if (waypoints.Length > 0)
        {
            url += "&waypoints=" + string.Join("%7C", waypoints.Select(Encode));
        }

        return url + $"&travelmode={options.Value.TravelMode}";
    }

    /// <summary>"53.12345,18.01234" → "53.12345%2C18.01234".</summary>
    private static string Encode(GeoPoint point) => Uri.EscapeDataString(point.ToString());
}
