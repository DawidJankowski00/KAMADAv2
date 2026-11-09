namespace Kamada.Modules.Ordering.Application.Payments;

internal enum OrderPaymentState
{
    /// <summary>Order is waiting for payment (status <c>New</c>).</summary>
    AwaitingPayment,

    /// <summary>Order has been paid and progresses through fulfilment.</summary>
    Paid,

    /// <summary>Order was cancelled; a completed payment may be refunded.</summary>
    Cancelled,
}

/// <summary>What a payment needs to know about its order.</summary>
internal sealed record OrderForPayment(Guid OrderId, long Number, decimal TotalAmount, OrderPaymentState State);
