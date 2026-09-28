using System.ComponentModel.DataAnnotations;
using Kamada.Modules.RouteOptimization.Domain.Solvers;

namespace Kamada.Modules.RouteOptimization.Application;

internal enum MatrixProviderKind
{
    /// <summary>OSRM <c>/table</c> service – self-hosted container or the public demo server. No API key.</summary>
    Osrm,

    /// <summary>Google Routes API <c>computeRouteMatrix</c> – requires <see cref="GoogleRoutesOptions.ApiKey"/>.</summary>
    Google,

    /// <summary>Straight-line estimate; no network. Always approximate.</summary>
    Haversine,
}

internal sealed class RouteOptimizationOptions : IValidatableObject
{
    public const string SectionName = "RouteOptimization";

    public MatrixProviderKind MatrixProvider { get; init; } = MatrixProviderKind.Osrm;

    /// <summary>Use the Haversine estimate (flagged as approximate) when the provider is unavailable.</summary>
    public bool FallbackToHaversine { get; init; } = true;

    public TimeSpan MatrixCacheDuration { get; init; } = TimeSpan.FromHours(6);

    [Range(1, 200)]
    public int MaxStops { get; init; } = 60;

    [Range(0, BruteForceSolver.MaxStops)]
    public int BruteForceMaxStops { get; init; } = 8;

    [Range(0, HeldKarpSolver.MaxStops)]
    public int HeldKarpMaxStops { get; init; } = 15;

    public TimeSpan HeuristicTimeLimit { get; init; } = TimeSpan.FromSeconds(1);

    public OsrmOptions Osrm { get; init; } = new();

    public GoogleRoutesOptions Google { get; init; } = new();

    public HaversineOptions Haversine { get; init; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (HeldKarpMaxStops < BruteForceMaxStops)
        {
            yield return new ValidationResult("HeldKarpMaxStops must not be lower than BruteForceMaxStops.", [nameof(HeldKarpMaxStops)]);
        }

        if (HeuristicTimeLimit <= TimeSpan.Zero)
        {
            yield return new ValidationResult("HeuristicTimeLimit must be positive.", [nameof(HeuristicTimeLimit)]);
        }

        if (MatrixProvider == MatrixProviderKind.Google && string.IsNullOrWhiteSpace(Google.ApiKey))
        {
            yield return new ValidationResult(
                "The Google matrix provider requires RouteOptimization:Google:ApiKey (user secrets / env var).", [nameof(Google)]);
        }

        if (Haversine.CircuityFactor < 1 || Haversine.AverageSpeedKmh <= 0)
        {
            yield return new ValidationResult("Haversine needs CircuityFactor ≥ 1 and a positive AverageSpeedKmh.", [nameof(Haversine)]);
        }
    }
}

internal sealed class OsrmOptions
{
    /// <summary>Self-hosted OSRM (docker compose profile) or the public demo server for development.</summary>
    public Uri BaseUrl { get; init; } = new("https://router.project-osrm.org/");

    public string Profile { get; init; } = "driving";

    /// <summary>The public demo server allows at most 1 request/second; use zero for a self-hosted instance.</summary>
    public TimeSpan MinDelayBetweenRequests { get; init; } = TimeSpan.FromSeconds(1);
}

internal sealed class GoogleRoutesOptions
{
    public Uri BaseUrl { get; init; } = new("https://routes.googleapis.com/");

    public string? ApiKey { get; init; }
}

internal sealed class HaversineOptions
{
    /// <summary>Road distance / straight-line distance; ~1.3 is typical for cities.</summary>
    public double CircuityFactor { get; init; } = 1.3;

    public double AverageSpeedKmh { get; init; } = 30;
}
