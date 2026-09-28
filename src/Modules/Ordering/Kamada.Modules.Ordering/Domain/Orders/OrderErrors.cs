using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Ordering.Domain.Orders;

internal static class OrderErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Orders.NotFound", "Order was not found.");

    public static readonly Error Empty =
        Error.Validation("Orders.Empty", "An order must contain at least one item.");

    public static readonly Error InvalidLine =
        Error.Validation("Orders.InvalidLine", "Every item must have a positive quantity and price.");

    public static readonly Error TotalTooLarge =
        Error.Validation("Orders.TotalTooLarge", $"Order total cannot exceed {Order.MaxTotal}.");

    public static Error InvalidTransition(OrderStatus from, OrderStatus to) =>
        Error.Conflict("Orders.InvalidTransition", $"Order cannot change status from {from} to {to}.");
}
