using System.Globalization;

namespace Kamada.Shared.Kernel.Geography;

/// <summary>
/// WGS84 coordinate (degrees). Always valid: construction fails for out-of-range values.
/// </summary>
public readonly record struct GeoPoint
{
    public GeoPoint(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "Latitude must be between -90 and 90.");
        }

        if (double.IsNaN(longitude) || longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "Longitude must be between -180 and 180.");
        }

        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }

    public double Longitude { get; }

    public static bool IsValid(double latitude, double longitude) =>
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;

    /// <summary>"lat,lng" with invariant culture, 6 decimals (~10 cm) – the format used by map URLs.</summary>
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"{Latitude:0.######},{Longitude:0.######}");
}
