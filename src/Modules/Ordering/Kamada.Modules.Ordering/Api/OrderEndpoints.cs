using Kamada.Modules.Ordering.Application.Orders;
using Kamada.Shared.Contracts.Common;
using Kamada.Shared.Contracts.Ordering;
using Kamada.Shared.Infrastructure.Auth;
using Kamada.Shared.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kamada.Modules.Ordering.Api;

internal static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/orders")
            .WithTags("Orders")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        group.MapGet("/", async ([AsParameters] OrderListQuery query, OrderQueries queries, CancellationToken ct) =>
                TypedResults.Ok(await queries.ListAsync(query, ct)))
            .WithValidation<OrderListQuery>()
            .WithSummary("List orders filtered by status, client, date range or number/recipient")
            .Produces<PagedResult<OrderSummaryDto>>();

        group.MapGet("/status-counts", async (OrderQueries queries, CancellationToken ct) =>
                TypedResults.Ok(await queries.CountByStatusAsync(ct)))
            .WithSummary("Number of orders per status (dashboard)")
            .Produces<IReadOnlyList<OrderStatusCountDto>>();

        group.MapGet("/{id:guid}", async (Guid id, OrderQueries queries, CancellationToken ct) =>
                (await queries.GetAsync(id, ct)).ToHttpResult(TypedResults.Ok))
            .WithName("GetOrder")
            .Produces<OrderDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (PlaceOrderRequest request, PlaceOrderHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(request, ct))
                    .ToHttpResult(order => TypedResults.CreatedAtRoute(order, "GetOrder", new { id = order.Id })))
            .WithValidation<PlaceOrderRequest>()
            .WithSummary("Place an order: reserves stock and freezes prices in one transaction")
            .Produces<OrderDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/ready-for-delivery", async (Guid id, OrderCommands commands, CancellationToken ct) =>
                (await commands.MarkReadyForDeliveryAsync(id, ct)).ToHttpResult(TypedResults.NoContent))
            .WithSummary("Paid → ReadyForDelivery (packed, can be added to a delivery run)")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/cancel", async (Guid id, OrderCommands commands, CancellationToken ct) =>
                (await commands.CancelAsync(id, ct)).ToHttpResult(TypedResults.NoContent))
            .WithSummary("Cancel and return items to stock")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
