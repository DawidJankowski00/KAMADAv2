using Kamada.Modules.Delivery.Application.Runs;
using Kamada.Shared.Contracts.Delivery;
using Kamada.Shared.Infrastructure.Auth;
using Kamada.Shared.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kamada.Modules.Delivery.Api;

/// <summary>Courier app API: always scoped to the signed-in courier (other couriers' runs are 404).</summary>
internal static class CourierRunEndpoints
{
    public static IEndpointRouteBuilder MapCourierRunEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/courier/delivery-runs")
            .WithTags("Courier")
            .RequireAuthorization(AuthorizationPolicies.CourierOnly);

        group.MapGet("/", async ([AsParameters] CourierRunListQuery query, DeliveryRunQueries queries, ICurrentUser user, CancellationToken ct) =>
                TypedResults.Ok(await queries.ListForCourierAsync(user.Id, query.Scope, ct)))
            .WithValidation<CourierRunListQuery>()
            .WithSummary("My runs: current (today / in progress), upcoming or completed")
            .Produces<IReadOnlyList<DeliveryRunSummaryDto>>();

        group.MapGet("/{id:guid}", async (Guid id, DeliveryRunQueries queries, ICurrentUser user, CancellationToken ct) =>
                (await queries.GetForCourierAsync(id, user.Id, ct)).ToHttpResult(TypedResults.Ok))
            .WithSummary("My run with stops, recipients, items, arrival estimates and navigation links")
            .Produces<DeliveryRunDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/start", async (Guid id, CourierRunCommands commands, ICurrentUser user, CancellationToken ct) =>
                (await commands.StartAsync(id, user.Id, ct)).ToHttpResult(TypedResults.Ok))
            .WithSummary("Planned → InProgress (orders go out for delivery)")
            .Produces<DeliveryRunDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/stops/{stopId:guid}/deliver", async (Guid id, Guid stopId, CourierRunCommands commands, ICurrentUser user, CancellationToken ct) =>
                (await commands.MarkDeliveredAsync(id, stopId, user.Id, ct)).ToHttpResult(TypedResults.Ok))
            .WithSummary("Mark a stop delivered (the run completes after its last stop)")
            .Produces<DeliveryRunDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/stops/{stopId:guid}/fail", async (Guid id, Guid stopId, FailStopRequest request, CourierRunCommands commands, ICurrentUser user, CancellationToken ct) =>
                (await commands.MarkFailedAsync(id, stopId, request, user.Id, ct)).ToHttpResult(TypedResults.Ok))
            .WithValidation<FailStopRequest>()
            .WithSummary("Mark a stop failed with a reason (the run completes after its last stop)")
            .Produces<DeliveryRunDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/complete", async (Guid id, CourierRunCommands commands, ICurrentUser user, CancellationToken ct) =>
                (await commands.CompleteAsync(id, user.Id, ct)).ToHttpResult(TypedResults.Ok))
            .WithSummary("Finish the run early; stops not visited are recorded as failed")
            .Produces<DeliveryRunDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
