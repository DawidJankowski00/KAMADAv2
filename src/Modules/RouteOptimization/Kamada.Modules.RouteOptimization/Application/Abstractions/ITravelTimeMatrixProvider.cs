using Kamada.Modules.RouteOptimization.Domain;
using Kamada.Shared.Kernel.Geography;

namespace Kamada.Modules.RouteOptimization.Application.Abstractions;

/// <summary>
/// Travel durations/distances between all pairs of <paramref name="points"/> in one request
/// ((n+2)² pairs – instead of asking a map service about n! complete routes).
/// </summary>
internal interface ITravelTimeMatrixProvider
{
    /// <returns>Matrix indexed like <paramref name="points"/>.</returns>
    Task<TravelMatrix> GetMatrixAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken);
}
