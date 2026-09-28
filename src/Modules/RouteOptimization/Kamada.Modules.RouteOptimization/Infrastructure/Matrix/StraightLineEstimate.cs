using Kamada.Modules.RouteOptimization.Application;
using Kamada.Shared.Kernel.Geography;

namespace Kamada.Modules.RouteOptimization.Infrastructure.Matrix;

/// <summary>Great-circle distance × circuity factor, driven at an average speed.</summary>
internal static class StraightLineEstimate
{
    private const double EarthRadiusMeters = 6_371_008.8;

    public static double HaversineMeters(GeoPoint a, GeoPoint b)
    {
        var dLat = ToRadians(b.Latitude - a.Latitude);
        var dLng = ToRadians(b.Longitude - a.Longitude);
        var h = (Math.Sin(dLat / 2) * Math.Sin(dLat / 2))
            + (Math.Cos(ToRadians(a.Latitude)) * Math.Cos(ToRadians(b.Latitude)) * Math.Sin(dLng / 2) * Math.Sin(dLng / 2));
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    public static (double DurationSeconds, double DistanceMeters) Estimate(GeoPoint from, GeoPoint to, HaversineOptions options)
    {
        var distance = HaversineMeters(from, to) * options.CircuityFactor;
        return (distance / (options.AverageSpeedKmh / 3.6), distance);
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
