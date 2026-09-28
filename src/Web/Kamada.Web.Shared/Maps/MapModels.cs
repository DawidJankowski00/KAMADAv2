namespace Kamada.Web.Shared.Maps;

public sealed record MapPoint(double Latitude, double Longitude)
{
    /// <summary>Gdańsk city centre – default view when there is nothing to show yet.</summary>
    public static MapPoint DefaultCenter { get; } = new(54.3520, 18.6466);
}

public enum MapMarkerKind
{
    Stop,
    Start,
    End,
    Done,
    Failed,
    Pin,
}

/// <param name="Number">Text inside the marker (stop sequence); start/end show S/K.</param>
public sealed record MapMarker(double Latitude, double Longitude, string? Label = null, MapMarkerKind Kind = MapMarkerKind.Stop, string? Number = null);
