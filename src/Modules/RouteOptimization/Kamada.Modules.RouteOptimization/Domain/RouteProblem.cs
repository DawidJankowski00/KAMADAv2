namespace Kamada.Modules.RouteOptimization.Domain;

/// <summary>
/// Fixed-start, fixed-end TSP on a <see cref="TravelMatrix"/>: every index other than start and end is a stop.
/// Start and end may be the same index (round trip). The objective is total duration.
/// </summary>
internal static class RouteProblem
{
    public static int[] Stops(TravelMatrix matrix, int startIndex, int endIndex)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
        ArgumentOutOfRangeException.ThrowIfNegative(endIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(startIndex, matrix.Size);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(endIndex, matrix.Size);

        return [.. Enumerable.Range(0, matrix.Size).Where(i => i != startIndex && i != endIndex)];
    }

    public static double Duration(TravelMatrix matrix, int startIndex, int endIndex, IReadOnlyList<int> stopOrder) =>
        Sum(matrix.DurationsSeconds, startIndex, endIndex, stopOrder);

    public static double Distance(TravelMatrix matrix, int startIndex, int endIndex, IReadOnlyList<int> stopOrder) =>
        Sum(matrix.DistancesMeters, startIndex, endIndex, stopOrder);

    public static RouteSolution Solution(
        TravelMatrix matrix, int startIndex, int endIndex, IReadOnlyList<int> stopOrder, string algorithm, bool isOptimal) =>
        new(
            stopOrder,
            Duration(matrix, startIndex, endIndex, stopOrder),
            Distance(matrix, startIndex, endIndex, stopOrder),
            algorithm,
            isOptimal);

    /// <summary>True when <paramref name="stopOrder"/> visits every stop exactly once.</summary>
    public static bool IsPermutationOfStops(TravelMatrix matrix, int startIndex, int endIndex, IReadOnlyList<int> stopOrder)
    {
        var stops = Stops(matrix, startIndex, endIndex);
        return stopOrder.Count == stops.Length && stopOrder.Order().SequenceEqual(stops);
    }

    private static double Sum(double[,] values, int startIndex, int endIndex, IReadOnlyList<int> stopOrder)
    {
        var total = 0d;
        var previous = startIndex;
        foreach (var stop in stopOrder)
        {
            total += values[previous, stop];
            previous = stop;
        }

        return total + values[previous, endIndex];
    }
}
