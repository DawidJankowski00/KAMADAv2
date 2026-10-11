using Kamada.Modules.Ordering.Application.Orders;
using Kamada.Shared.Contracts.Auth;
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
            .WithTags("Orders");

        // Policies of nested groups add up, so the endpoints sellers may call get their own group.
        var sales = group.MapGroup("/").RequireAuthorization(AuthorizationPolicies.SalesAccess);
        var admin = group.MapGroup("/").RequireAuthorization(AuthorizationPolicies.AdminOnly);

        admin.MapGet("/", async ([AsParameters] OrderListQuery query, OrderQueries queries, CancellationToken ct) =>
                TypedResults.Ok(await queries.ListAsync(query, ct)))
            .WithValidation<OrderListQuery>()
            .WithSummary("List orders filtered by status, client, date range or number/recipient")
            .Produces<PagedResult<OrderSummaryDto>>();

        sales.MapGet("/mine", async ([AsParameters] OrderListQuery query, OrderQueries queries, ICurrentUser user, CancellationToken ct) =>
                TypedResults.Ok(await queries.ListAsync(query with { PlacedBy = user.Id }, ct)))
            .WithValidation<OrderListQuery>()
            .WithSummary("Orders placed by the signed-in worker (the seller's order history)")
            .Produces<PagedResult<OrderSummaryDto>>();

        admin.MapGet("/status-counts", async (OrderQueries queries, CancellationToken ct) =>
                TypedResults.Ok(await queries.CountByStatusAsync(ct)))
            .WithSummary("Number of orders per status (dashboard)")
            .Produces<IReadOnlyList<OrderStatusCountDto>>();

        sales.MapGet("/{id:guid}", async (Guid id, OrderQueries queries, ICurrentUser user, CancellationToken ct) =>
                (await queries.GetAsync(id, user.IsInRole(Roles.Admin) ? null : user.Id, ct)).ToHttpResult(TypedResults.Ok))
            .WithName("GetOrder")
            .WithSummary("Order details; a seller gets 404 for orders placed by someone else")
            .Produces<OrderDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        sales.MapPost("/", async (PlaceOrderRequest request, PlaceOrderHandler handler, ICurrentUser user, CancellationToken ct) =>
                (await handler.HandleAsync(request, user.Id, ct))
                    .ToHttpResult(order => TypedResults.CreatedAtRoute(order, "GetOrder", new { id = order.Id })))
            .WithValidation<PlaceOrderRequest>()
            .WithSummary("Place an order: reserves stock and freezes prices in one transaction")
            .Produces<OrderDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapPost("/{id:guid}/ready-for-delivery", async (Guid id, OrderCommands commands, CancellationToken ct) =>
                (await commands.MarkReadyForDeliveryAsync(id, ct)).ToHttpResult(TypedResults.NoContent))
            .WithSummary("Paid → ReadyForDelivery (packed, can be added to a delivery run)")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapPost("/{id:guid}/cancel", async (Guid id, OrderCommands commands, CancellationToken ct) =>
                (await commands.CancelAsync(id, ct)).ToHttpResult(TypedResults.NoContent))
            .WithSummary("Cancel and return items to stock")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
