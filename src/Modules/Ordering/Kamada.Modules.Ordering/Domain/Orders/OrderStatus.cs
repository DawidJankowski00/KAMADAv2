namespace Kamada.Modules.Ordering.Domain.Orders;

internal enum OrderStatus
{
    New,
    Paid,
    ReadyForDelivery,
    AssignedToRun,
    OutForDelivery,
    Delivered,
    DeliveryFailed,
    Cancelled,
}
