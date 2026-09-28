using Kamada.Modules.Payments.Application;
using Kamada.Shared.Contracts.Payments;
using Kamada.Shared.Infrastructure.Auth;
using Kamada.Shared.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kamada.Modules.Payments.Api;

internal static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var orderPayments = endpoints.MapGroup("/api/orders/{orderId:guid}/payments")
            .WithTags("Payments")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        orderPayments.MapGet("/", async (Guid orderId, PaymentService payments, CancellationToken ct) =>
                TypedResults.Ok(await payments.ListForOrderAsync(orderId, ct)))
            .Produces<IReadOnlyList<PaymentDto>>();

        orderPayments.MapPost("/", async (Guid orderId, RegisterPaymentRequest request, PaymentService payments, CancellationToken ct) =>
                (await payments.RegisterAsync(orderId, request, ct))
                    .ToHttpResult(payment => TypedResults.Created($"/api/payments/{payment.Id}", payment)))
            .WithValidation<RegisterPaymentRequest>()
            .WithSummary("Register a payment with the simulated provider (a declined card is recorded as Failed)")
            .Produces<PaymentDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        var group = endpoints.MapGroup("/api/payments")
            .WithTags("Payments")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        group.MapGet("/{id:guid}", async (Guid id, PaymentService payments, CancellationToken ct) =>
                (await payments.GetAsync(id, ct)).ToHttpResult(TypedResults.Ok))
            .Produces<PaymentDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/confirm", async (Guid id, PaymentService payments, CancellationToken ct) =>
                (await payments.ConfirmTransferAsync(id, ct)).ToHttpResult(TypedResults.Ok))
            .WithSummary("Confirm that a bank transfer arrived (order becomes Paid)")
            .Produces<PaymentDto>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/reject", async (Guid id, PaymentService payments, CancellationToken ct) =>
                (await payments.RejectTransferAsync(id, ct)).ToHttpResult(TypedResults.Ok))
            .WithSummary("Mark a pending bank transfer as failed")
            .Produces<PaymentDto>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/refund", async (Guid id, PaymentService payments, CancellationToken ct) =>
                (await payments.RefundAsync(id, ct)).ToHttpResult(TypedResults.Ok))
            .WithSummary("Refund a completed payment of a cancelled order")
            .Produces<PaymentDto>()
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
