using System.Globalization;
using Kamada.Modules.RouteOptimization.Domain;
using Kamada.Modules.RouteOptimization.Domain.Solvers;

namespace Kamada.Benchmarks;

/// <summary>
/// How far from the optimum are the heuristic and the unoptimized input order? Prints a Markdown table
/// (average and worst gap versus Held–Karp over many random asymmetric matrices).
/// </summary>
internal static class QualityReport
{
    private const int Samples = 100;

    public static void Print()
    {
        var culture = CultureInfo.InvariantCulture;
        var heldKarp = new HeldKarpSolver();
        Console.WriteLine("| Stops | Samples | Heuristic avg gap | Heuristic worst gap | Heuristic optimal | Input order avg gap |");
        Console.WriteLine("|---:|---:|---:|---:|---:|---:|");

        foreach (var stops in new[] { 8, 10, 12, 15 })
        {
            var heuristicGaps = new List<double>();
            var inputGaps = new List<double>();
            for (var seed = 0; seed < Samples; seed++)
            {
                var matrix = BenchmarkMatrices.Random(stops + 2, seed);
                var end = stops + 1;
                var optimum = heldKarp.Solve(matrix, 0, end).TotalDurationSeconds;
                heuristicGaps.Add((Solvers.Heuristic.Solve(matrix, 0, end).TotalDurationSeconds / optimum) - 1);
                inputGaps.Add((RouteProblem.Duration(matrix, 0, end, RouteProblem.Stops(matrix, 0, end)) / optimum) - 1);
            }

            Console.WriteLine(string.Create(culture,
                $"| {stops} | {Samples} | {heuristicGaps.Average():P2} | {heuristicGaps.Max():P2} | {heuristicGaps.Count(g => g < 1e-9)}/{Samples} | {inputGaps.Average():P1} |"));
        }
    }
}
