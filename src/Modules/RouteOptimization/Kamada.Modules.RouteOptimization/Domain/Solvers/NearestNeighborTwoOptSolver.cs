namespace Kamada.Modules.RouteOptimization.Domain.Solvers;

/// <summary>
/// Heuristic for larger runs: nearest-neighbour construction, then local search with 2-opt (segment reversal)
/// and Or-opt (moving a chain of 1–3 stops) until no move improves the route or the time limit is reached.
/// The starting route is the better of nearest neighbour and the input order, so the result is never worse
/// than the input order. Works on asymmetric matrices (reversal deltas account for direction). Not optimal.
/// </summary>
internal sealed class NearestNeighborTwoOptSolver(TimeSpan timeLimit, TimeProvider clock) : IRouteSolver
{
    public const string AlgorithmName = "NearestNeighbor+2opt+OrOpt";

    private const double Epsilon = 1e-9;
    private const int MaxOrOptChain = 3;

    public string Name => AlgorithmName;

    public RouteSolution Solve(TravelMatrix matrix, int startIndex, int endIndex)
    {
        var stops = RouteProblem.Stops(matrix, startIndex, endIndex);
        if (stops.Length <= 1)
        {
            return RouteProblem.Solution(matrix, startIndex, endIndex, stops, AlgorithmName, isOptimal: true);
        }

        var started = clock.GetTimestamp();
        var d = matrix.DurationsSeconds;

        var nearest = NearestNeighbor(d, startIndex, stops);
        var initial = RouteProblem.Duration(matrix, startIndex, endIndex, nearest)
            < RouteProblem.Duration(matrix, startIndex, endIndex, stops)
                ? nearest
                : stops;

        // path = start, stops…, end; positions 1..n are movable.
        var path = new int[initial.Length + 2];
        path[0] = startIndex;
        initial.CopyTo(path, 1);
        path[^1] = endIndex;

        var local = new LocalSearch(d, path);
        bool improved;
        do
        {
            improved = local.TwoOpt(() => clock.GetElapsedTime(started) > timeLimit);
            improved |= local.OrOpt(() => clock.GetElapsedTime(started) > timeLimit);
        }
        while (improved && clock.GetElapsedTime(started) <= timeLimit);

        return RouteProblem.Solution(matrix, startIndex, endIndex, path[1..^1], AlgorithmName, isOptimal: false);
    }

    private static int[] NearestNeighbor(double[,] d, int startIndex, int[] stops)
    {
        var order = new int[stops.Length];
        var visited = new bool[stops.Length];
        var current = startIndex;
        for (var position = 0; position < stops.Length; position++)
        {
            var best = -1;
            for (var k = 0; k < stops.Length; k++)
            {
                if (!visited[k] && (best < 0 || d[current, stops[k]] < d[current, stops[best]]))
                {
                    best = k;
                }
            }

            visited[best] = true;
            order[position] = stops[best];
            current = stops[best];
        }

        return order;
    }

    private sealed class LocalSearch
    {
        private readonly double[,] _d;
        private readonly int[] _path;

        // _forward[k] = cost of path[0..k] driven forward; _backward[k] = the same edges driven in reverse.
        private readonly double[] _forward;
        private readonly double[] _backward;

        public LocalSearch(double[,] d, int[] path)
        {
            _d = d;
            _path = path;
            _forward = new double[path.Length];
            _backward = new double[path.Length];
            RecomputePrefixes();
        }

        private int LastStop => _path.Length - 2;

        /// <summary>Reverses path[i..j] when that shortens the route (first improvement).</summary>
        public bool TwoOpt(Func<bool> timeIsUp)
        {
            var improvedAny = false;
            for (var i = 1; i < LastStop; i++)
            {
                if (timeIsUp())
                {
                    return improvedAny;
                }

                for (var j = i + 1; j <= LastStop; j++)
                {
                    var before = _d[_path[i - 1], _path[i]] + (_forward[j] - _forward[i]) + _d[_path[j], _path[j + 1]];
                    var after = _d[_path[i - 1], _path[j]] + (_backward[j] - _backward[i]) + _d[_path[i], _path[j + 1]];
                    if (after < before - Epsilon)
                    {
                        Array.Reverse(_path, i, j - i + 1);
                        RecomputePrefixes();
                        improvedAny = true;
                    }
                }
            }

            return improvedAny;
        }

        /// <summary>Moves a chain path[i..i+len-1] (kept in direction) between two other consecutive points.</summary>
        public bool OrOpt(Func<bool> timeIsUp)
        {
            var improvedAny = false;
            for (var length = 1; length <= MaxOrOptChain; length++)
            {
                for (var i = 1; i + length - 1 <= LastStop; i++)
                {
                    if (timeIsUp())
                    {
                        return improvedAny;
                    }

                    var e = i + length - 1;
                    var removalGain = _d[_path[i - 1], _path[i]] + _d[_path[e], _path[e + 1]] - _d[_path[i - 1], _path[e + 1]];

                    for (var k = 0; k <= LastStop; k++)
                    {
                        if (k >= i - 1 && k <= e)
                        {
                            continue;
                        }

                        var insertion = _d[_path[k], _path[i]] + _d[_path[e], _path[k + 1]] - _d[_path[k], _path[k + 1]];
                        if (insertion < removalGain - Epsilon)
                        {
                            MoveChain(i, e, k);
                            RecomputePrefixes();
                            improvedAny = true;
                            break;
                        }
                    }
                }
            }

            return improvedAny;
        }

        /// <summary>Moves path[i..e] so that it follows the point currently at position k.</summary>
        private void MoveChain(int i, int e, int k)
        {
            var chain = _path[i..(e + 1)];
            var rest = _path.Where((_, position) => position < i || position > e).ToList();
            var insertAfter = k < i ? k : k - chain.Length;
            rest.InsertRange(insertAfter + 1, chain);
            rest.CopyTo(_path);
        }

        private void RecomputePrefixes()
        {
            _forward[0] = 0;
            _backward[0] = 0;
            for (var k = 1; k < _path.Length; k++)
            {
                _forward[k] = _forward[k - 1] + _d[_path[k - 1], _path[k]];
                _backward[k] = _backward[k - 1] + _d[_path[k], _path[k - 1]];
            }
        }
    }
}
