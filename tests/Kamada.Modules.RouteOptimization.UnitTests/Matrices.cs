using Kamada.Modules.RouteOptimization.Domain;

namespace Kamada.Modules.RouteOptimization.UnitTests;

internal static class Matrices
{
    /// <summary>Durations as given; distances = durations × 10 (so totals are easy to check).</summary>
    public static TravelMatrix FromDurations(double[,] durations)
    {
        var n = durations.GetLength(0);
        var distances = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                distances[i, j] = durations[i, j] * 10;
            }
        }

        return new TravelMatrix(durations, distances, "Test", isApproximate: false);
    }

    /// <summary>
    /// Asymmetric matrix: points on a plane (triangle-inequality-ish base) plus random one-way penalties,
    /// deterministic for a given seed.
    /// </summary>
    public static TravelMatrix Random(int size, int seed)
    {
        var random = new Random(seed);
        var xs = Enumerable.Range(0, size).Select(_ => random.NextDouble() * 10_000).ToArray();
        var ys = Enumerable.Range(0, size).Select(_ => random.NextDouble() * 10_000).ToArray();
        var durations = new double[size, size];
        for (var i = 0; i < size; i++)
        {
            for (var j = 0; j < size; j++)
            {
                if (i != j)
                {
                    var straight = Math.Sqrt(Math.Pow(xs[i] - xs[j], 2) + Math.Pow(ys[i] - ys[j], 2));
                    durations[i, j] = Math.Round((straight / 8) + (random.NextDouble() * 120), 1);
                }
            }
        }

        return FromDurations(durations);
    }
}
