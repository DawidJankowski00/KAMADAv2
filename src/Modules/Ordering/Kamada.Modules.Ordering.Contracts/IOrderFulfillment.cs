using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Ordering.Contracts;

/// <summary>Order status as seen by the Delivery module.</summary>
public enum FulfillmentStatus
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

/// <summary>Status changes the Delivery module drives.</summary>
public enum DeliveryTransition
{
    /// <summary><c>ReadyForDelivery → AssignedToRun</c>: added to a delivery run.</summary>
    AssignToRun,

    /// <summary><c>AssignedToRun → ReadyForDelivery</c>: removed from a run or the run was cancelled.</summary>
    RemoveFromRun,

    /// <summary><c>AssignedToRun → OutForDelivery</c>: the courier started the run.</summary>
    OutForDelivery,

    /// <summary><c>OutForDelivery → Delivered</c>.</summary>
    Delivered,

    /// <summary><c>OutForDelivery → DeliveryFailed</c>.</summary>
    DeliveryFailed,
}

public sealed record DeliveryOrderLine(string ProductName, int Quantity);

/// <summary>What a courier needs to know about an order (recipient snapshot taken when the order was placed).</summary>
public sealed record DeliveryOrder(
    Guid OrderId,
    long Number,
    FulfillmentStatus Status,
    Guid AddressId,
    string RecipientName,
    string PhoneNumber,
    string Street,
    string BuildingNumber,
    string? ApartmentNumber,
    string City,
    string PostalCode,
    double? Latitude,
    double? Longitude,
    string? Notes,
    decimal TotalAmount,
    IReadOnlyList<DeliveryOrderLine> Items);

/// <summary>What the Delivery module needs from Ordering.</summary>
public interface IOrderFulfillment
{
    /// <summary>Orders that do not exist are left out of the result.</summary>
    Task<IReadOnlyList<DeliveryOrder>> GetDeliveryOrdersAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken);

    /// <summary>
    /// Applies the transition to all orders or none (fails without saving when any order is missing or in the
    /// wrong status). Call inside <c>IUnitOfWork</c> together with saving the delivery run.
    /// </summary>
    Task<Result> ApplyAsync(IReadOnlyCollection<Guid> orderIds, DeliveryTransition transition, CancellationToken cancellationToken);
}
