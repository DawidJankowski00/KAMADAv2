namespace Kamada.Modules.RouteOptimization.Domain.Solvers;

/// <summary>
/// Held–Karp dynamic programming over subsets: <c>cost[S, j]</c> = cheapest way to leave the start, visit exactly
/// the stops in <c>S</c> and stand at stop <c>j</c>. Exact, O(n²·2ⁿ) time and O(n·2ⁿ) memory.
/// </summary>
internal sealed class HeldKarpSolver : IRouteSolver
{
    public const string AlgorithmName = "HeldKarp";

    /// <summary>2¹⁸ subsets × 18 stops ≈ 38 MB of costs – a hard guard, the selector uses a lower threshold.</summary>
    public const int MaxStops = 18;

    public string Name => AlgorithmName;

    public RouteSolution Solve(TravelMatrix matrix, int startIndex, int endIndex)
    {
        var stops = RouteProblem.Stops(matrix, startIndex, endIndex);
        var n = stops.Length;
        if (n > MaxStops)
        {
            throw new ArgumentException($"Held-Karp supports at most {MaxStops} stops, got {n}.", nameof(matrix));
        }

        if (n == 0)
        {
            return RouteProblem.Solution(matrix, startIndex, endIndex, [], AlgorithmName, isOptimal: true);
        }

        var d = matrix.DurationsSeconds;
        var subsets = 1 << n;
        var cost = new double[subsets * n];
        var parent = new byte[subsets * n];
        Array.Fill(cost, double.PositiveInfinity);

        for (var j = 0; j < n; j++)
        {
            cost[((1 << j) * n) + j] = d[startIndex, stops[j]];
        }

        for (var subset = 1; subset < subsets; subset++)
        {
            for (var last = 0; last < n; last++)
            {
                var current = cost[(subset * n) + last];
                if ((subset & (1 << last)) == 0 || double.IsPositiveInfinity(current))
                {
                    continue;
                }

                for (var next = 0; next < n; next++)
                {
                    if ((subset & (1 << next)) != 0)
                    {
                        continue;
                    }

                    var extended = subset | (1 << next);
                    var candidate = current + d[stops[last], stops[next]];
                    if (candidate < cost[(extended * n) + next])
                    {
                        cost[(extended * n) + next] = candidate;
                        parent[(extended * n) + next] = (byte)last;
                    }
                }
            }
        }

        var all = subsets - 1;
        var bestLast = 0;
        var bestCost = double.PositiveInfinity;
        for (var last = 0; last < n; last++)
        {
            var total = cost[(all * n) + last] + d[stops[last], endIndex];
            if (total < bestCost)
            {
                bestCost = total;
                bestLast = last;
            }
        }

        var order = new int[n];
        var mask = all;
        var node = bestLast;
        for (var position = n - 1; position >= 0; position--)
        {
            order[position] = stops[node];
            var previous = parent[(mask * n) + node];
            mask &= ~(1 << node);
            node = previous;
        }

        return RouteProblem.Solution(matrix, startIndex, endIndex, order, AlgorithmName, isOptimal: true);
    }
}
