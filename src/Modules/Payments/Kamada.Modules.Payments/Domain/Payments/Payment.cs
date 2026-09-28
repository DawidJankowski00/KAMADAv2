using Kamada.Shared.Kernel.Domain;
using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Payments.Domain.Payments;

internal enum PaymentProvider
{
    Cash,
    Card,
    Transfer,
}

internal enum PaymentStatus
{
    Pending,
    Completed,
    Failed,
    Refunded,
}

/// <summary>
/// A payment attempt for an order. <c>Pending → Completed → Refunded</c> or <c>Pending → Failed</c>.
/// At most one Pending/Completed payment may exist per order (enforced by a partial unique index as well).
/// </summary>
internal sealed class Payment : Entity, IAuditable
{
    private Payment(Guid id, Guid orderId, PaymentProvider provider, string transactionId, decimal amount)
        : base(id)
    {
        OrderId = orderId;
        Provider = provider;
        TransactionId = transactionId;
        Amount = amount;
        Status = PaymentStatus.Pending;
    }

    public Guid OrderId { get; private set; }

    public PaymentProvider Provider { get; private set; }

    public string TransactionId { get; private set; }

    public decimal Amount { get; private set; }

    public PaymentStatus Status { get; private set; }

    public string? FailureReason { get; private set; }

    public DateTimeOffset? PaidAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public bool IsActive => Status is PaymentStatus.Pending or PaymentStatus.Completed;

    public static Payment Start(Guid orderId, PaymentProvider provider, string transactionId, decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Payment amount must be positive.");
        }

        return new Payment(Guid.CreateVersion7(), orderId, provider, transactionId, amount);
    }

    public Result Complete(DateTimeOffset now)
    {
        if (Status != PaymentStatus.Pending)
        {
            return PaymentErrors.InvalidTransition(Status, PaymentStatus.Completed);
        }

        Status = PaymentStatus.Completed;
        PaidAt = now;
        return Result.Success();
    }

    public Result Fail(string reason)
    {
        if (Status != PaymentStatus.Pending)
        {
            return PaymentErrors.InvalidTransition(Status, PaymentStatus.Failed);
        }

        Status = PaymentStatus.Failed;
        FailureReason = reason;
        return Result.Success();
    }

    public Result Refund()
    {
        if (Status != PaymentStatus.Completed)
        {
            return PaymentErrors.InvalidTransition(Status, PaymentStatus.Refunded);
        }

        Status = PaymentStatus.Refunded;
        return Result.Success();
    }
}
