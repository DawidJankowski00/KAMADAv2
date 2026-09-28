using Kamada.Modules.Payments.Application.Abstractions;
using Kamada.Modules.Payments.Domain.Payments;
using Kamada.Modules.Payments.Infrastructure;
using Microsoft.Extensions.Time.Testing;

namespace Kamada.Modules.Payments.UnitTests;

public sealed class PaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    private static Payment NewPayment() => Payment.Start(Guid.NewGuid(), PaymentProvider.Card, "TX-1", 120.50m);

    [Fact]
    public void Start_IsPending()
    {
        var payment = NewPayment();

        payment.Status.ShouldBe(PaymentStatus.Pending);
        payment.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Complete_SetsPaidAt_ThenRefund()
    {
        var payment = NewPayment();

        payment.Complete(Now).IsSuccess.ShouldBeTrue();
        payment.PaidAt.ShouldBe(Now);
        payment.Refund().IsSuccess.ShouldBeTrue();
        payment.Status.ShouldBe(PaymentStatus.Refunded);
        payment.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Fail_StoresReasonAndBlocksCompletion()
    {
        var payment = NewPayment();

        payment.Fail("declined").IsSuccess.ShouldBeTrue();

        payment.FailureReason.ShouldBe("declined");
        payment.Complete(Now).Error.Code.ShouldBe("Payments.InvalidTransition");
    }

    [Fact]
    public void Refund_PendingPayment_Fails()
    {
        NewPayment().Refund().Error.Code.ShouldBe("Payments.InvalidTransition");
    }

    [Theory]
    [InlineData(nameof(PaymentProvider.Cash), false, nameof(GatewayOutcome.Approved))]
    [InlineData(nameof(PaymentProvider.Card), false, nameof(GatewayOutcome.Approved))]
    [InlineData(nameof(PaymentProvider.Card), true, nameof(GatewayOutcome.Declined))]
    [InlineData(nameof(PaymentProvider.Transfer), false, nameof(GatewayOutcome.AwaitingConfirmation))]
    public async Task SimulatedGateway_OutcomeDependsOnProvider(string providerName, bool decline, string expectedOutcome)
    {
        var provider = Enum.Parse<PaymentProvider>(providerName);
        var expected = Enum.Parse<GatewayOutcome>(expectedOutcome);
        var gateway = new SimulatedPaymentGateway(new FakeTimeProvider(Now));

        var response = await gateway.ProcessAsync(
            new GatewayRequest(Guid.NewGuid(), 42, 10m, provider, decline),
            TestContext.Current.CancellationToken);

        response.Outcome.ShouldBe(expected);
        response.TransactionId.ShouldStartWith($"{provider.ToString().ToUpperInvariant()}-20260928-");
    }
}
