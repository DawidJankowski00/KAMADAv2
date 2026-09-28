namespace Kamada.Shared.Contracts.Payments;

public enum PaymentProvider
{
    /// <summary>Cash collected by staff – completed immediately.</summary>
    Cash,

    /// <summary>Card terminal (simulated) – completed or declined immediately.</summary>
    Card,

    /// <summary>Bank transfer (simulated) – pending until the admin confirms receipt.</summary>
    Transfer,
}

public enum PaymentStatus
{
    Pending,
    Completed,
    Failed,
    Refunded,
}

public sealed record PaymentDto(
    Guid Id,
    Guid OrderId,
    PaymentProvider Provider,
    string TransactionId,
    decimal Amount,
    PaymentStatus Status,
    string? FailureReason,
    DateTimeOffset? PaidAt,
    DateTimeOffset CreatedAt);

/// <param name="SimulateDecline">Demo switch: makes the simulated card terminal decline the payment.</param>
public sealed record RegisterPaymentRequest(PaymentProvider Provider, bool SimulateDecline = false);
