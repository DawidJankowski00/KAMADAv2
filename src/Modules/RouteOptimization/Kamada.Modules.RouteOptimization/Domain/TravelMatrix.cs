namespace Kamada.Modules.RouteOptimization.Domain;

/// <summary>
/// Travel durations and distances between every ordered pair of points: <c>[from, to]</c>.
/// May be asymmetric (one-way streets, turn restrictions), the diagonal is zero.
/// </summary>
internal sealed class TravelMatrix
{
    public TravelMatrix(double[,] durationsSeconds, double[,] distancesMeters, string provider, bool isApproximate)
    {
        ArgumentNullException.ThrowIfNull(durationsSeconds);
        ArgumentNullException.ThrowIfNull(distancesMeters);
        ArgumentException.ThrowIfNullOrWhiteSpace(provider);

        var size = durationsSeconds.GetLength(0);
        if (durationsSeconds.GetLength(1) != size || distancesMeters.GetLength(0) != size || distancesMeters.GetLength(1) != size)
        {
            throw new ArgumentException("Duration and distance matrices must be square and of the same size.", nameof(distancesMeters));
        }

        for (var i = 0; i < size; i++)
        {
            for (var j = 0; j < size; j++)
            {
                if (!double.IsFinite(durationsSeconds[i, j]) || durationsSeconds[i, j] < 0
                    || !double.IsFinite(distancesMeters[i, j]) || distancesMeters[i, j] < 0)
                {
                    throw new ArgumentException($"Matrix values must be finite and non-negative (at [{i}, {j}]).", nameof(durationsSeconds));
                }
            }
        }

        DurationsSeconds = durationsSeconds;
        DistancesMeters = distancesMeters;
        Provider = provider;
        IsApproximate = isApproximate;
    }

    public double[,] DurationsSeconds { get; }

    public double[,] DistancesMeters { get; }

    public int Size => DurationsSeconds.GetLength(0);

    /// <summary>Name of the source (<c>OSRM</c>, <c>Google</c>, <c>Haversine</c>).</summary>
    public string Provider { get; }

    /// <summary>Straight-line estimate instead of road network travel times.</summary>
    public bool IsApproximate { get; }

    public double Duration(int from, int to) => DurationsSeconds[from, to];

    public double Distance(int from, int to) => DistancesMeters[from, to];

    /// <summary>Same values, marked as approximate (e.g. some pairs had to be estimated).</summary>
    public TravelMatrix AsApproximate() =>
        IsApproximate ? this : new TravelMatrix(DurationsSeconds, DistancesMeters, Provider, isApproximate: true);
}
