using BenchmarkDotNet.Attributes;
using Kamada.Modules.RouteOptimization.Domain;
using Kamada.Modules.RouteOptimization.Domain.Solvers;

namespace Kamada.Benchmarks;

/// <summary>Small runs: all three solvers (brute force is only feasible here).</summary>
[MemoryDiagnoser]
public class SmallRunBenchmarks
{
    private TravelMatrix _matrix = null!;

    [Params(6, 8, 10)]
    public int Stops { get; set; }

    [GlobalSetup]
    public void Setup() => _matrix = BenchmarkMatrices.Random(Stops + 2, seed: 42);

    [Benchmark(Baseline = true)]
    public double BruteForce() => new BruteForceSolver().Solve(_matrix, 0, Stops + 1).TotalDurationSeconds;

    [Benchmark]
    public double HeldKarp() => new HeldKarpSolver().Solve(_matrix, 0, Stops + 1).TotalDurationSeconds;

    [Benchmark]
    public double Heuristic() => Solvers.Heuristic.Solve(_matrix, 0, Stops + 1).TotalDurationSeconds;
}

/// <summary>Medium runs: the exact Held–Karp versus the heuristic.</summary>
[MemoryDiagnoser]
public class MediumRunBenchmarks
{
    private TravelMatrix _matrix = null!;

    [Params(12, 15)]
    public int Stops { get; set; }

    [GlobalSetup]
    public void Setup() => _matrix = BenchmarkMatrices.Random(Stops + 2, seed: 42);

    [Benchmark(Baseline = true)]
    public double HeldKarp() => new HeldKarpSolver().Solve(_matrix, 0, Stops + 1).TotalDurationSeconds;

    [Benchmark]
    public double Heuristic() => Solvers.Heuristic.Solve(_matrix, 0, Stops + 1).TotalDurationSeconds;
}

/// <summary>Large runs: only the heuristic is practical (time to converge, no time limit hit).</summary>
[MemoryDiagnoser]
public class LargeRunBenchmarks
{
    private TravelMatrix _matrix = null!;

    [Params(25, 50, 100)]
    public int Stops { get; set; }

    [GlobalSetup]
    public void Setup() => _matrix = BenchmarkMatrices.Random(Stops + 2, seed: 42);

    [Benchmark]
    public double Heuristic() => Solvers.Heuristic.Solve(_matrix, 0, Stops + 1).TotalDurationSeconds;
}

internal static class Solvers
{
    /// <summary>Generous limit so benchmarks measure convergence, not the time budget.</summary>
    public static readonly NearestNeighborTwoOptSolver Heuristic = new(TimeSpan.FromSeconds(30), TimeProvider.System);
}
