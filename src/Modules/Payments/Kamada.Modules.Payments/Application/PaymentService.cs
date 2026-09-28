using Kamada.Modules.Ordering.Contracts;
using Kamada.Modules.Payments.Application.Abstractions;
using Kamada.Modules.Payments.Domain.Payments;
using Kamada.Shared.Contracts.Payments;
using Kamada.Shared.Infrastructure.Persistence;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;
using DomainProvider = Kamada.Modules.Payments.Domain.Payments.PaymentProvider;
using PaymentStatus = Kamada.Modules.Payments.Domain.Payments.PaymentStatus;

namespace Kamada.Modules.Payments.Application;

/// <summary>
/// Payment use cases. Whenever a payment completes, the order is marked as paid in the same transaction.
/// </summary>
internal sealed class PaymentService(
    IPaymentsDbContext db,
    IPaymentGateway gateway,
    IOrderPayments orders,
    IUnitOfWork unitOfWork,
    TimeProvider clock)
{
    public async Task<Result<PaymentDto>> RegisterAsync(Guid orderId, RegisterPaymentRequest request, CancellationToken cancellationToken)
    {
        var order = await orders.GetOrderForPaymentAsync(orderId, cancellationToken);
        if (order.IsFailure)
        {
            return order.Error;
        }

        if (order.Value.State != OrderPaymentState.AwaitingPayment)
        {
            return PaymentErrors.OrderNotAwaitingPayment;
        }

        if (await db.Payments.AnyAsync(p => p.OrderId == orderId && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Completed), cancellationToken))
        {
            return PaymentErrors.AlreadyInProgress;
        }

        var provider = Enum.Parse<DomainProvider>(request.Provider.ToString());
        var response = await gateway.ProcessAsync(
            new GatewayRequest(orderId, order.Value.Number, order.Value.TotalAmount, provider, request.SimulateDecline),
            cancellationToken);

        var payment = Payment.Start(orderId, provider, response.TransactionId, order.Value.TotalAmount);
        switch (response.Outcome)
        {
            case GatewayOutcome.Approved:
                payment.Complete(clock.GetUtcNow());
                break;
            case GatewayOutcome.Declined:
                payment.Fail(response.DeclineReason ?? "Declined by provider.");
                break;
        }

        var saved = await unitOfWork.ExecuteAsync(
            async ct =>
            {
                db.Payments.Add(payment);
                await db.SaveChangesAsync(ct);
                return payment.Status == PaymentStatus.Completed
                    ? await orders.MarkPaidAsync(orderId, ct)
                    : Result.Success();
            },
            cancellationToken);

        return saved.IsSuccess ? payment.ToDto() : saved.Error;
    }

    /// <summary>Bank transfer arrived: complete the payment and mark the order as paid.</summary>
    public async Task<Result<PaymentDto>> ConfirmTransferAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await db.Payments.SingleOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        if (payment is null)
        {
            return PaymentErrors.NotFound;
        }

        if (payment.Provider != DomainProvider.Transfer)
        {
            return PaymentErrors.NotATransfer;
        }

        var result = await unitOfWork.ExecuteAsync(
            async ct =>
            {
                var completed = payment.Complete(clock.GetUtcNow());
                if (completed.IsFailure)
                {
                    return completed;
                }

                await db.SaveChangesAsync(ct);
                return await orders.MarkPaidAsync(payment.OrderId, ct);
            },
            cancellationToken);

        return result.IsSuccess ? payment.ToDto() : result.Error;
    }

    public async Task<Result<PaymentDto>> RejectTransferAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await db.Payments.SingleOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        if (payment is null)
        {
            return PaymentErrors.NotFound;
        }

        if (payment.Provider != DomainProvider.Transfer)
        {
            return PaymentErrors.NotATransfer;
        }

        var result = payment.Fail("Transfer was not received.");
        if (result.IsFailure)
        {
            return result.Error;
        }

        await db.SaveChangesAsync(cancellationToken);
        return payment.ToDto();
    }

    /// <summary>Refund of a completed payment – allowed only once the order has been cancelled.</summary>
    public async Task<Result<PaymentDto>> RefundAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await db.Payments.SingleOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        if (payment is null)
        {
            return PaymentErrors.NotFound;
        }

        var order = await orders.GetOrderForPaymentAsync(payment.OrderId, cancellationToken);
        if (order.IsFailure)
        {
            return order.Error;
        }

        if (order.Value.State != OrderPaymentState.Cancelled)
        {
            return PaymentErrors.RefundRequiresCancelledOrder;
        }

        var result = payment.Refund();
        if (result.IsFailure)
        {
            return result.Error;
        }

        await db.SaveChangesAsync(cancellationToken);
        return payment.ToDto();
    }

    public async Task<IReadOnlyList<PaymentDto>> ListForOrderAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var payments = await db.Payments.AsNoTracking()
            .Where(p => p.OrderId == orderId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
        return [.. payments.Select(p => p.ToDto())];
    }

    public async Task<Result<PaymentDto>> GetAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        var payment = await db.Payments.AsNoTracking().SingleOrDefaultAsync(p => p.Id == paymentId, cancellationToken);
        return payment is null ? PaymentErrors.NotFound : payment.ToDto();
    }
}
