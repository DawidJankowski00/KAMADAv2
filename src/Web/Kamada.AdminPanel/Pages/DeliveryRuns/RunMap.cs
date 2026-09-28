using Kamada.Shared.Contracts.Delivery;
using Kamada.Web.Shared;
using Kamada.Web.Shared.Maps;

namespace Kamada.AdminPanel.Pages.DeliveryRuns;

/// <summary>Map markers and the route polyline (straight segments start → stops → end) for a delivery run.</summary>
internal static class RunMap
{
    public static IReadOnlyList<MapMarker> Markers(DeliveryRunDto run, IReadOnlyList<DeliveryStopDto> orderedStops)
    {
        var markers = new List<MapMarker>();
        var sameEndpoints = run.StartLocation.Id == run.EndLocation.Id;

        if (Point(run.StartLocation) is { } start)
        {
            var label = sameEndpoints ? $"Start i koniec: {run.StartLocation.Name}" : $"Start: {run.StartLocation.Name}";
            markers.Add(new MapMarker(start.Latitude, start.Longitude, label, MapMarkerKind.Start));
        }

        for (var i = 0; i < orderedStops.Count; i++)
        {
            var stop = orderedStops[i];
            if (!stop.HasCoordinates)
            {
                continue;
            }

            var kind = stop.Status switch
            {
                DeliveryStopStatus.Delivered => MapMarkerKind.Done,
                DeliveryStopStatus.Failed => MapMarkerKind.Failed,
                _ => MapMarkerKind.Stop,
            };
            var number = (i + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            markers.Add(new MapMarker(stop.Latitude!.Value, stop.Longitude!.Value, $"{number}. #{stop.OrderNumber} {stop.RecipientName} – {Labels.Format(stop)}", kind, number));
        }

        if (!sameEndpoints && Point(run.EndLocation) is { } end)
        {
            markers.Add(new MapMarker(end.Latitude, end.Longitude, $"Koniec: {run.EndLocation.Name}", MapMarkerKind.End));
        }

        return markers;
    }

    public static IReadOnlyList<MapPoint> Route(DeliveryRunDto run, IReadOnlyList<DeliveryStopDto> orderedStops)
    {
        var points = new List<MapPoint>();
        if (Point(run.StartLocation) is { } start)
        {
            points.Add(start);
        }

        points.AddRange(orderedStops.Where(s => s.HasCoordinates).Select(s => new MapPoint(s.Latitude!.Value, s.Longitude!.Value)));

        if (Point(run.EndLocation) is { } end)
        {
            points.Add(end);
        }

        return points;
    }

    /// <summary>Human-readable name of a navigation link point: 0 = start, 1..n = stops, n + 1 = end.</summary>
    public static string PointName(int point, int stopCount) =>
        point == 0 ? "start" : point > stopCount ? "koniec" : $"przystanek {point}";

    private static MapPoint? Point(RouteLocationDto location) =>
        location.HasCoordinates ? new MapPoint(location.Latitude!.Value, location.Longitude!.Value) : null;
}
