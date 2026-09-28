using BenchmarkDotNet.Running;
using Kamada.Benchmarks;

// dotnet run -c Release --project benchmarks/Kamada.Benchmarks -- --filter *          (timings)
// dotnet run -c Release --project benchmarks/Kamada.Benchmarks -- --quality           (heuristic vs optimum)
if (args.Contains("--quality"))
{
    QualityReport.Print();
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(SmallRunBenchmarks).Assembly).Run(args);
