using System.ComponentModel.DataAnnotations;

namespace Kamada.Modules.RouteOptimization.Infrastructure.GoogleMaps;

internal sealed class GoogleMapsOptions
{
    public const string SectionName = "GoogleMaps";

    /// <summary>
    /// Waypoints (between origin and destination) per link. Google Maps URLs docs: up to 9 in the Google Maps app
    /// and desktop browsers, only 3 in mobile browsers – lower this if couriers open links in a mobile browser.
    /// </summary>
    [Range(1, 9)]
    public int MaxWaypointsPerLink { get; init; } = 9;

    [RegularExpression("^(driving|walking|bicycling|two-wheeler|transit)$")]
    public string TravelMode { get; init; } = "driving";
}
