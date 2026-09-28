using Kamada.Modules.Payments.Domain.Payments;

namespace Kamada.Modules.Payments.Application.Abstractions;

internal enum GatewayOutcome
{
    /// <summary>Money received (cash, approved card).</summary>
    Approved,

    /// <summary>Rejected by the provider (e.g. card declined).</summary>
    Declined,

    /// <summary>Asynchronous method: the payment stays pending until confirmed (bank transfer).</summary>
    AwaitingConfirmation,
}

internal sealed record GatewayRequest(Guid OrderId, long OrderNumber, decimal Amount, PaymentProvider Provider, bool SimulateDecline);

internal sealed record GatewayResponse(GatewayOutcome Outcome, string TransactionId, string? DeclineReason = null);

/// <summary>
/// Port to a payment provider. The project ships a simulated implementation; a real one (PayU, Stripe…)
/// would implement the same interface.
/// </summary>
internal interface IPaymentGateway
{
    Task<GatewayResponse> ProcessAsync(GatewayRequest request, CancellationToken cancellationToken);
}
