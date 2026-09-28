using Kamada.Modules.Delivery.Application.Runs;
using Kamada.Shared.Contracts.Common;
using Kamada.Shared.Contracts.Delivery;
using Kamada.Shared.Infrastructure.Auth;
using Kamada.Shared.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kamada.Modules.Delivery.Api;

internal static class DeliveryRunEndpoints
{
    public static IEndpointRouteBuilder MapDeliveryRunEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/delivery-runs")
            .WithTags("Delivery runs")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        group.MapGet("/", async ([AsParameters] DeliveryRunListQuery query, DeliveryRunQueries queries, CancellationToken ct) =>
                TypedResults.Ok(await queries.ListAsync(query, ct)))
            .WithValidation<DeliveryRunListQuery>()
            .WithSummary("List runs filtered by status, courier, planned date or name/plate")
            .Produces<PagedResult<DeliveryRunSummaryDto>>();

        group.MapGet("/{id:guid}", async (Guid id, DeliveryRunQueries queries, CancellationToken ct) =>
                (await queries.GetAsync(id, ct)).ToHttpResult(TypedResults.Ok))
            .WithName("GetDeliveryRun")
            .Produces<DeliveryRunDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (CreateDeliveryRunRequest request, DeliveryRunCommands commands, CancellationToken ct) =>
                (await commands.CreateAsync(request, ct))
                    .ToHttpResult(run => TypedResults.CreatedAtRoute(run, "GetDeliveryRun", new { id = run.Id })))
            .WithValidation<CreateDeliveryRunRequest>()
            .WithSummary("Create a draft run, optionally with orders (must be ReadyForDelivery)")
            .Produces<DeliveryRunDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (Guid id, UpdateDeliveryRunRequest request, DeliveryRunCommands commands, CancellationToken ct) =>
                (await commands.UpdateAsync(id, request, ct)).ToHttpResult(TypedResults.Ok))
            .WithValidation<UpdateDeliveryRunRequest>()
            .WithSummary("Change name, date, courier, vehicle or endpoints of a draft run")
            .Produces<DeliveryRunDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/orders", async (Guid id, AddRunOrdersRequest request, DeliveryRunCommands commands, CancellationToken ct) =>
                (await commands.AddOrdersAsync(id, request, ct)).ToHttpResult(TypedResults.Ok))
            .WithValidation<AddRunOrdersRequest>()
            .WithSummary("Add orders (ReadyForDelivery → AssignedToRun); clears the stored route")
            .Produces<DeliveryRunDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}/orders/{orderId:guid}", async (Guid id, Guid orderId, DeliveryRunCommands commands, CancellationToken ct) =>
                (await commands.RemoveOrderAsync(id, orderId, ct)).ToHttpResult(TypedResults.Ok))
            .WithSummary("Remove an order (back to ReadyForDelivery); clears the stored route")
            .Produces<DeliveryRunDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/optimize", async (Guid id, RoutePlanningHandler planning, CancellationToken ct) =>
                (await planning.PreviewAsync(id, ct)).ToHttpResult(TypedResults.Ok))
            .WithSummary("Compute the optimal stop order and compare it with the current one (nothing is saved)")
            .Produces<RouteOptimizationPreviewDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/apply-optimization", async (Guid id, RoutePlanningHandler planning, CancellationToken ct) =>
                (await planning.ApplyOptimizationAsync(id, ct)).ToHttpResult(TypedResults.Ok))
            .WithSummary("Optimize and store the stop order, arrival estimates and Google Maps links")
            .Produces<DeliveryRunDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/sequence", async (Guid id, SetRunSequenceRequest request, RoutePlanningHandler planning, CancellationToken ct) =>
                (await planning.SetSequenceAsync(id, request, ct)).ToHttpResult(TypedResults.Ok))
            .WithValidation<SetRunSequenceRequest>()
            .WithSummary("Store a manual stop order (every order exactly once) with recomputed times and links")
            .Produces<DeliveryRunDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/approve", async (Guid id, DeliveryRunCommands commands, CancellationToken ct) =>
                (await commands.ApproveAsync(id, ct)).ToHttpResult(TypedResults.Ok))
            .WithSummary("Draft → Planned: the courier sees the run from now on")
            .Produces<DeliveryRunDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/cancel", async (Guid id, DeliveryRunCommands commands, CancellationToken ct) =>
                (await commands.CancelAsync(id, ct)).ToHttpResult(TypedResults.Ok))
            .WithSummary("Cancel a run that has not started; its orders become ready for delivery again")
            .Produces<DeliveryRunDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
