using Kamada.Modules.Ordering.Domain.Orders;

namespace Kamada.Modules.Ordering.UnitTests;

public sealed class OrderTests
{
    private static readonly ShippingAddress Address = new(
        Guid.NewGuid(), "Jan Kowalski", "+48 600 000 000", "Gdańska", "10", null, "Bydgoszcz", "85-005", "Polska", 53.12, 18.0);

    private static OrderLine Line(int quantity, decimal price) =>
        new(Guid.NewGuid(), "Produkt", "SKU-1", quantity, price);

    private static Order PlaceOrder(params OrderLine[] lines) =>
        Order.Place(Guid.NewGuid(), Address, lines.Length == 0 ? [Line(1, 10m)] : lines, notes: null).Value;

    private static Order OrderIn(OrderStatus status)
    {
        var order = PlaceOrder();
        Func<Order, Kamada.Shared.Kernel.Results.Result>[] path = status switch
        {
            OrderStatus.New => [],
            OrderStatus.Paid => [o => o.MarkPaid()],
            OrderStatus.ReadyForDelivery => [o => o.MarkPaid(), o => o.MarkReadyForDelivery()],
            OrderStatus.AssignedToRun => [o => o.MarkPaid(), o => o.MarkReadyForDelivery(), o => o.AssignToRun()],
            OrderStatus.OutForDelivery => [o => o.MarkPaid(), o => o.MarkReadyForDelivery(), o => o.AssignToRun(), o => o.MarkOutForDelivery()],
            OrderStatus.Delivered => [o => o.MarkPaid(), o => o.MarkReadyForDelivery(), o => o.AssignToRun(), o => o.MarkOutForDelivery(), o => o.MarkDelivered()],
            OrderStatus.DeliveryFailed => [o => o.MarkPaid(), o => o.MarkReadyForDelivery(), o => o.AssignToRun(), o => o.MarkOutForDelivery(), o => o.MarkDeliveryFailed()],
            OrderStatus.Cancelled => [o => o.Cancel()],
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

        foreach (var step in path)
        {
            step(order).IsSuccess.ShouldBeTrue();
        }

        order.Status.ShouldBe(status);
        return order;
    }

    [Fact]
    public void Place_ComputesTotalFromLines()
    {
        var order = PlaceOrder(Line(3, 19.99m), Line(2, 0.01m), Line(1, 100m));

        order.TotalAmount.ShouldBe(159.99m);
        order.Status.ShouldBe(OrderStatus.New);
        order.Items.Count.ShouldBe(3);
        order.Items.ShouldAllBe(i => i.OrderId == order.Id);
    }

    [Fact]
    public void Place_WithoutLines_Fails()
    {
        Order.Place(Guid.NewGuid(), Address, [], null).Error.ShouldBe(OrderErrors.Empty);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void Place_InvalidLine_Fails(int quantity, decimal price)
    {
        Order.Place(Guid.NewGuid(), Address, [Line(quantity, price)], null).Error.ShouldBe(OrderErrors.InvalidLine);
    }

    [Fact]
    public void Place_TotalAboveLimit_Fails()
    {
        Order.Place(Guid.NewGuid(), Address, [Line(2, 60_000_000m)], null).Error.ShouldBe(OrderErrors.TotalTooLarge);
    }

    [Fact]
    public void HappyPath_NewToDelivered()
    {
        OrderIn(OrderStatus.Delivered).Status.ShouldBe(OrderStatus.Delivered);
    }

    [Theory]
    [InlineData(nameof(OrderStatus.New), true)]
    [InlineData(nameof(OrderStatus.Paid), true)]
    [InlineData(nameof(OrderStatus.ReadyForDelivery), true)]
    [InlineData(nameof(OrderStatus.AssignedToRun), false)]
    [InlineData(nameof(OrderStatus.OutForDelivery), false)]
    [InlineData(nameof(OrderStatus.Delivered), false)]
    [InlineData(nameof(OrderStatus.DeliveryFailed), false)]
    [InlineData(nameof(OrderStatus.Cancelled), false)]
    public void Cancel_AllowedOnlyBeforeAssignmentToRun(string statusName, bool allowed)
    {
        var status = Enum.Parse<OrderStatus>(statusName);
        var order = OrderIn(status);

        var result = order.Cancel();

        result.IsSuccess.ShouldBe(allowed);
        order.Status.ShouldBe(allowed ? OrderStatus.Cancelled : status);
    }

    [Fact]
    public void MarkReadyForDelivery_UnpaidOrder_FailsWithConflict()
    {
        var order = OrderIn(OrderStatus.New);

        var result = order.MarkReadyForDelivery();

        result.Error.Code.ShouldBe("Orders.InvalidTransition");
        result.Error.Type.ShouldBe(Kamada.Shared.Kernel.Results.ErrorType.Conflict);
    }

    [Theory]
    [InlineData(nameof(OrderStatus.AssignedToRun), true)]
    [InlineData(nameof(OrderStatus.DeliveryFailed), true)]
    [InlineData(nameof(OrderStatus.Paid), false)]
    [InlineData(nameof(OrderStatus.OutForDelivery), false)]
    public void ReturnToReadyForDelivery_OnlyFromRunOrFailedDelivery(string statusName, bool allowed)
    {
        OrderIn(Enum.Parse<OrderStatus>(statusName)).ReturnToReadyForDelivery().IsSuccess.ShouldBe(allowed);
    }

    [Fact]
    public void TerminalStatuses_HaveNoOutgoingTransitions()
    {
        foreach (var target in Enum.GetValues<OrderStatus>())
        {
            Order.CanTransition(OrderStatus.Delivered, target).ShouldBeFalse();
            Order.CanTransition(OrderStatus.Cancelled, target).ShouldBeFalse();
        }
    }
}
