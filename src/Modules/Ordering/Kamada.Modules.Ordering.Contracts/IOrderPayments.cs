using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Ordering.Contracts;

public enum OrderPaymentState
{
    /// <summary>Order is waiting for payment (status <c>New</c>).</summary>
    AwaitingPayment,

    /// <summary>Order has been paid and progresses through fulfilment.</summary>
    Paid,

    /// <summary>Order was cancelled; a completed payment may be refunded.</summary>
    Cancelled,
}

public sealed record OrderForPayment(Guid OrderId, long Number, decimal TotalAmount, OrderPaymentState State);

/// <summary>What the Payments module needs from Ordering.</summary>
public interface IOrderPayments
{
    Task<Result<OrderForPayment>> GetOrderForPaymentAsync(Guid orderId, CancellationToken cancellationToken);

    /// <summary>Moves the order <c>New → Paid</c>. Call inside <c>IUnitOfWork</c> together with saving the payment.</summary>
    Task<Result> MarkPaidAsync(Guid orderId, CancellationToken cancellationToken);
}
