using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.RouteOptimization.Application;

internal static class RouteOptimizationErrors
{
    public static Error TooManyStops(int count, int max) => Error.Validation(
        "RouteOptimization.TooManyStops", $"A route can have at most {max} stops, got {count}.");

    public static readonly Error InvalidStopOrder = Error.Validation(
        "RouteOptimization.InvalidStopOrder", "The stop order must list every stop exactly once.");
}
