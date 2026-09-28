using Kamada.Modules.RouteOptimization.Domain;

namespace Kamada.Benchmarks;

internal static class BenchmarkMatrices
{
    /// <summary>
    /// Deterministic asymmetric matrix resembling city driving: points spread over ~10 × 10 km,
    /// ~8 m/s plus up to 2 minutes of random one-way penalty per ordered pair.
    /// </summary>
    public static TravelMatrix Random(int size, int seed)
    {
        var random = new Random(seed);
        var xs = Enumerable.Range(0, size).Select(_ => random.NextDouble() * 10_000).ToArray();
        var ys = Enumerable.Range(0, size).Select(_ => random.NextDouble() * 10_000).ToArray();
        var durations = new double[size, size];
        var distances = new double[size, size];
        for (var i = 0; i < size; i++)
        {
            for (var j = 0; j < size; j++)
            {
                if (i != j)
                {
                    distances[i, j] = Math.Sqrt(Math.Pow(xs[i] - xs[j], 2) + Math.Pow(ys[i] - ys[j], 2)) * 1.3;
                    durations[i, j] = (distances[i, j] / 8) + (random.NextDouble() * 120);
                }
            }
        }

        return new TravelMatrix(durations, distances, "Benchmark", isApproximate: false);
    }
}
