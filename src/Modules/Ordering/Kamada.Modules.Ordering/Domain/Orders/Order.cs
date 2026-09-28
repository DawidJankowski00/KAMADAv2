using Kamada.Shared.Kernel.Domain;
using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Ordering.Domain.Orders;

/// <summary>
/// Customer order (aggregate root). The total is always computed here from the lines – never taken from input –
/// and the status only changes through the transition methods below.
/// </summary>
/// <remarks>
/// <code>
/// New ──► Paid ──► ReadyForDelivery ──► AssignedToRun ──► OutForDelivery ──► Delivered
///  │        │            │  ▲                 │                  │
///  │        │            │  └─────────────────┘ (removed from run)└──► DeliveryFailed ──► ReadyForDelivery
///  └────────┴────────────┴──► Cancelled
/// </code>
/// </remarks>
internal sealed class Order : Entity, IAuditable
{
    public const decimal MaxTotal = 99_999_999.99m;

    private static readonly Dictionary<OrderStatus, OrderStatus[]> AllowedTransitions = new()
    {
        [OrderStatus.New] = [OrderStatus.Paid, OrderStatus.Cancelled],
        [OrderStatus.Paid] = [OrderStatus.ReadyForDelivery, OrderStatus.Cancelled],
        [OrderStatus.ReadyForDelivery] = [OrderStatus.AssignedToRun, OrderStatus.Cancelled],
        [OrderStatus.AssignedToRun] = [OrderStatus.ReadyForDelivery, OrderStatus.OutForDelivery],
        [OrderStatus.OutForDelivery] = [OrderStatus.Delivered, OrderStatus.DeliveryFailed],
        [OrderStatus.DeliveryFailed] = [OrderStatus.ReadyForDelivery],
        [OrderStatus.Delivered] = [],
        [OrderStatus.Cancelled] = [],
    };

    private readonly List<OrderItem> _items = [];

    // Only a scalar constructor: EF Core cannot bind complex properties through constructor parameters.
    private Order(Guid id)
        : base(id) => ShippingAddress = null!;

    /// <summary>Human-friendly sequential number (database identity).</summary>
    public long Number { get; private set; }

    public Guid ClientId { get; private set; }

    public ShippingAddress ShippingAddress { get; private set; }

    public OrderStatus Status { get; private set; }

    public decimal TotalAmount { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public IReadOnlyCollection<OrderItem> Items => _items;

    public static bool CanTransition(OrderStatus from, OrderStatus to) => AllowedTransitions[from].Contains(to);

    public static Result<Order> Place(Guid clientId, ShippingAddress shippingAddress, IReadOnlyCollection<OrderLine> lines, string? notes)
    {
        if (lines.Count == 0)
        {
            return OrderErrors.Empty;
        }

        if (lines.Any(l => l.Quantity <= 0 || l.UnitPrice <= 0))
        {
            return OrderErrors.InvalidLine;
        }

        var order = new Order(Guid.CreateVersion7())
        {
            ClientId = clientId,
            ShippingAddress = shippingAddress,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            Status = OrderStatus.New,
        };
        order._items.AddRange(lines.Select(line => OrderItem.Create(order.Id, line)));
        order.TotalAmount = CalculateTotal(order._items);

        return order.TotalAmount > MaxTotal ? OrderErrors.TotalTooLarge : order;
    }

    /// <summary>Sum of unit price × quantity; prices have 2 decimals so the sum is exact in <see cref="decimal"/>.</summary>
    public static decimal CalculateTotal(IEnumerable<OrderItem> items) => items.Sum(i => i.LineTotal);

    public Result MarkPaid() => TransitionTo(OrderStatus.Paid);

    public Result MarkReadyForDelivery() => TransitionTo(OrderStatus.ReadyForDelivery);

    public Result AssignToRun() => TransitionTo(OrderStatus.AssignedToRun);

    /// <summary>Removed from a delivery run before it started, or re-planned after a failed delivery.</summary>
    public Result ReturnToReadyForDelivery() =>
        Status is OrderStatus.AssignedToRun or OrderStatus.DeliveryFailed
            ? TransitionTo(OrderStatus.ReadyForDelivery)
            : OrderErrors.InvalidTransition(Status, OrderStatus.ReadyForDelivery);

    public Result MarkOutForDelivery() => TransitionTo(OrderStatus.OutForDelivery);

    public Result MarkDelivered() => TransitionTo(OrderStatus.Delivered);

    public Result MarkDeliveryFailed() => TransitionTo(OrderStatus.DeliveryFailed);

    public Result Cancel() => TransitionTo(OrderStatus.Cancelled);

    private Result TransitionTo(OrderStatus target)
    {
        if (!CanTransition(Status, target))
        {
            return OrderErrors.InvalidTransition(Status, target);
        }

        Status = target;
        return Result.Success();
    }
}
