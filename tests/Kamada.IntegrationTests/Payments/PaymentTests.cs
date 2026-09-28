using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Ordering;
using Kamada.Shared.Contracts.Payments;

namespace Kamada.IntegrationTests.Payments;

public sealed class PaymentTests(KamadaApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(HttpClient Admin, OrderDto Order)> NewOrderAsync()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var product = await admin.CreateProductAsync(price: 49.90m);
        return (admin, await admin.PlaceOrderAsync((product.Id, 2)));
    }

    private static async Task<PaymentDto> PayAsync(HttpClient admin, Guid orderId, PaymentProvider provider, bool decline = false)
    {
        var response = await admin.PostJsonAsync($"/api/orders/{orderId}/payments", new RegisterPaymentRequest(provider, decline));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.ReadAsAsync<PaymentDto>();
    }

    [Fact]
    public async Task Cash_CompletesPaymentForOrderTotal_AndMarksOrderPaid()
    {
        var (admin, order) = await NewOrderAsync();

        var payment = await PayAsync(admin, order.Id, PaymentProvider.Cash);

        payment.Status.ShouldBe(PaymentStatus.Completed);
        payment.Amount.ShouldBe(99.80m);
        payment.PaidAt.ShouldNotBeNull();
        (await admin.GetOrderAsync(order.Id)).Status.ShouldBe(OrderStatus.Paid);
    }

    [Fact]
    public async Task SecondPayment_ForPaidOrder_Returns409()
    {
        var (admin, order) = await NewOrderAsync();
        await PayAsync(admin, order.Id, PaymentProvider.Card);

        var second = await admin.PostJsonAsync($"/api/orders/{order.Id}/payments", new RegisterPaymentRequest(PaymentProvider.Cash));

        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task DeclinedCard_IsRecordedAsFailed_OrderStaysNew_RetryWorks()
    {
        var (admin, order) = await NewOrderAsync();

        var declined = await PayAsync(admin, order.Id, PaymentProvider.Card, decline: true);
        (await admin.GetOrderAsync(order.Id)).Status.ShouldBe(OrderStatus.New);
        var retry = await PayAsync(admin, order.Id, PaymentProvider.Card);

        declined.Status.ShouldBe(PaymentStatus.Failed);
        declined.FailureReason.ShouldNotBeNullOrWhiteSpace();
        retry.Status.ShouldBe(PaymentStatus.Completed);
        var history = await (await admin.GetAsync($"/api/orders/{order.Id}/payments", Ct)).ReadAsAsync<List<PaymentDto>>();
        history.Select(p => p.Status).ShouldBe([PaymentStatus.Failed, PaymentStatus.Completed]);
    }

    [Fact]
    public async Task Transfer_StaysPendingUntilConfirmed()
    {
        var (admin, order) = await NewOrderAsync();

        var pending = await PayAsync(admin, order.Id, PaymentProvider.Transfer);
        pending.Status.ShouldBe(PaymentStatus.Pending);
        (await admin.GetOrderAsync(order.Id)).Status.ShouldBe(OrderStatus.New);

        var confirmed = await admin.PostAsync($"/api/payments/{pending.Id}/confirm", null, Ct);

        confirmed.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await confirmed.ReadAsAsync<PaymentDto>()).Status.ShouldBe(PaymentStatus.Completed);
        (await admin.GetOrderAsync(order.Id)).Status.ShouldBe(OrderStatus.Paid);
    }

    [Fact]
    public async Task Refund_RequiresCancelledOrder()
    {
        var (admin, order) = await NewOrderAsync();
        var payment = await PayAsync(admin, order.Id, PaymentProvider.Card);

        var tooEarly = await admin.PostAsync($"/api/payments/{payment.Id}/refund", null, Ct);
        await admin.PostAsync($"/api/orders/{order.Id}/cancel", null, Ct);
        var refunded = await admin.PostAsync($"/api/payments/{payment.Id}/refund", null, Ct);

        (await tooEarly.ReadProblemCodeAsync()).ShouldBe("Payments.RefundRequiresCancelledOrder");
        (await refunded.ReadAsAsync<PaymentDto>()).Status.ShouldBe(PaymentStatus.Refunded);
    }

    [Fact]
    public async Task Payment_ForCancelledOrder_Returns409()
    {
        var (admin, order) = await NewOrderAsync();
        await admin.PostAsync($"/api/orders/{order.Id}/cancel", null, Ct);

        var response = await admin.PostJsonAsync($"/api/orders/{order.Id}/payments", new RegisterPaymentRequest(PaymentProvider.Cash));

        (await response.ReadProblemCodeAsync()).ShouldBe("Payments.OrderNotAwaitingPayment");
    }
}
