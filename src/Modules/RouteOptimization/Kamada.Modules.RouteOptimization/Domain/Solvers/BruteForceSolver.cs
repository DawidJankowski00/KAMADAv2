namespace Kamada.Modules.RouteOptimization.Domain.Solvers;

/// <summary>
/// Exhaustive search over all n! stop orders (depth-first, pruning partial routes already worse than the best
/// complete one). Exact; practical up to ~10 stops.
/// </summary>
internal sealed class BruteForceSolver : IRouteSolver
{
    public const string AlgorithmName = "BruteForce";

    /// <summary>11! ≈ 40 million orders – a hard guard, the selector uses a much lower threshold.</summary>
    public const int MaxStops = 11;

    public string Name => AlgorithmName;

    public RouteSolution Solve(TravelMatrix matrix, int startIndex, int endIndex)
    {
        var stops = RouteProblem.Stops(matrix, startIndex, endIndex);
        if (stops.Length > MaxStops)
        {
            throw new ArgumentException($"Brute force supports at most {MaxStops} stops, got {stops.Length}.", nameof(matrix));
        }

        var search = new Search(matrix.DurationsSeconds, stops, endIndex)
        {
            // The input order is the first incumbent, so ties keep the caller's order.
            BestCost = RouteProblem.Duration(matrix, startIndex, endIndex, stops),
            Best = [.. stops],
        };
        search.Run(depth: 0, previous: startIndex, cost: 0);

        return RouteProblem.Solution(matrix, startIndex, endIndex, search.Best, AlgorithmName, isOptimal: true);
    }

    private sealed class Search(double[,] durations, int[] stops, int endIndex)
    {
        private readonly int[] _current = new int[stops.Length];
        private readonly bool[] _used = new bool[stops.Length];

        public required double BestCost { get; set; }

        public required int[] Best { get; set; }

        public void Run(int depth, int previous, double cost)
        {
            if (cost >= BestCost)
            {
                return;
            }

            if (depth == stops.Length)
            {
                var total = cost + durations[previous, endIndex];
                if (total < BestCost)
                {
                    BestCost = total;
                    Best = [.. _current];
                }

                return;
            }

            for (var k = 0; k < stops.Length; k++)
            {
                if (_used[k])
                {
                    continue;
                }

                _used[k] = true;
                _current[depth] = stops[k];
                Run(depth + 1, stops[k], cost + durations[previous, stops[k]]);
                _used[k] = false;
            }
        }
    }
}
