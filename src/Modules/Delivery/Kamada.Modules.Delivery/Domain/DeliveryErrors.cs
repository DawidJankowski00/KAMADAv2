using Kamada.Modules.Delivery.Domain.Runs;
using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Delivery.Domain;

internal static class DeliveryErrors
{
    public static readonly Error LocationNotFound =
        Error.NotFound("Delivery.LocationNotFound", "Route location was not found.");

    public static readonly Error LocationInactive =
        Error.Conflict("Delivery.LocationInactive", "The route location is deactivated.");

    public static readonly Error RunNotFound =
        Error.NotFound("Delivery.RunNotFound", "Delivery run was not found.");

    public static readonly Error StopNotFound =
        Error.NotFound("Delivery.StopNotFound", "The stop does not belong to this delivery run.");

    public static readonly Error OrderNotInRun =
        Error.NotFound("Delivery.OrderNotInRun", "The order is not part of this delivery run.");

    public static readonly Error OrderAlreadyInThisRun =
        Error.Conflict("Delivery.OrderAlreadyInThisRun", "The order is already part of this delivery run.");

    public static readonly Error RunHasNoStops =
        Error.Conflict("Delivery.RunHasNoStops", "Add at least one order to the run first.");

    public static readonly Error RouteNotPlanned =
        Error.Conflict("Delivery.RouteNotPlanned", "Optimize the route or set the stop order before approving the run.");

    public static readonly Error SequenceMustContainAllOrders =
        Error.Validation("Delivery.InvalidSequence", "The stop order must contain every order of the run exactly once.");

    public static readonly Error FailureReasonRequired =
        Error.Validation("Delivery.FailureReasonRequired", "Give the reason why the delivery failed.");

    public static Error TooManyStops(int max) =>
        Error.Validation("Delivery.TooManyStops", $"A delivery run can have at most {max} stops.");

    public static Error OrdersNotFound(IEnumerable<Guid> orderIds) =>
        Error.Validation("Delivery.OrdersNotFound", $"Orders not found: {string.Join(", ", orderIds)}.");

    public static Error OrdersNotReady(IEnumerable<long> orderNumbers) =>
        Error.Conflict("Delivery.OrdersNotReady", $"Orders {FormatNumbers(orderNumbers)} are not ready for delivery (already in a run, not paid or cancelled).");

    public static Error StopsWithoutCoordinates(IEnumerable<long> orderNumbers) =>
        Error.Validation("Delivery.StopsWithoutCoordinates", $"Orders {FormatNumbers(orderNumbers)} have delivery addresses without coordinates. Set the pin on the client's address first.");

    public static Error LocationWithoutCoordinates(string name) =>
        Error.Validation("Delivery.LocationWithoutCoordinates", $"Route location '{name}' has no coordinates. Set its pin on the map first.");

    public static Error RunNotEditable(DeliveryRunStatus status) =>
        Error.Conflict("Delivery.RunNotEditable", $"Only draft runs can be changed (the run is {status}).");

    public static Error RunNotInProgress(DeliveryRunStatus status) =>
        Error.Conflict("Delivery.RunNotInProgress", $"Stops can be completed only while the run is in progress (the run is {status}).");

    public static Error StopAlreadyCompleted(DeliveryStopStatus status) =>
        Error.Conflict("Delivery.StopAlreadyCompleted", $"The stop is already {status}.");

    public static Error InvalidTransition(DeliveryRunStatus from, DeliveryRunStatus to) =>
        Error.Conflict("Delivery.InvalidTransition", $"Delivery run cannot change status from {from} to {to}.");

    private static string FormatNumbers(IEnumerable<long> numbers) => string.Join(", ", numbers.Order().Select(n => $"#{n}"));
}
