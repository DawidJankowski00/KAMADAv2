using System.Globalization;
using Kamada.Modules.Ordering.Application.Abstractions;
using Kamada.Modules.Ordering.Domain.Payments;

namespace Kamada.Modules.Ordering.Infrastructure.Payments;

/// <summary>
/// Demo provider: cash is approved, card is approved unless a decline is requested, transfer waits for manual
/// confirmation. Transaction ids look like <c>CARD-20260928-3F9A1C2B</c>.
/// </summary>
internal sealed class SimulatedPaymentGateway(TimeProvider clock) : IPaymentGateway
{
    public Task<GatewayResponse> ProcessAsync(GatewayRequest request, CancellationToken cancellationToken)
    {
        var transactionId = string.Create(
            CultureInfo.InvariantCulture,
            $"{request.Provider.ToString().ToUpperInvariant()}-{clock.GetUtcNow():yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}");

        var response = request.Provider switch
        {
            PaymentProvider.Cash => new GatewayResponse(GatewayOutcome.Approved, transactionId),
            PaymentProvider.Card when request.SimulateDecline =>
                new GatewayResponse(GatewayOutcome.Declined, transactionId, "Card declined by issuer (simulated)."),
            PaymentProvider.Card => new GatewayResponse(GatewayOutcome.Approved, transactionId),
            PaymentProvider.Transfer => new GatewayResponse(GatewayOutcome.AwaitingConfirmation, transactionId),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Provider, "Unknown provider."),
        };

        return Task.FromResult(response);
    }
}
