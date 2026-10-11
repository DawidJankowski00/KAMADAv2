using Kamada.Modules.Workforce.Application.Workers;
using Kamada.Shared.Contracts.Common;
using Kamada.Shared.Contracts.Workforce;
using Kamada.Shared.Infrastructure.Auth;
using Kamada.Shared.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kamada.Modules.Workforce.Api;

internal static class WorkerEndpoints
{
    public static IEndpointRouteBuilder MapWorkerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/workers")
            .WithTags("Workers")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        group.MapGet("/", async ([AsParameters] WorkerListQuery query, WorkerQueries queries, CancellationToken ct) =>
                TypedResults.Ok(await queries.ListAsync(query, ct)))
            .WithValidation<WorkerListQuery>()
            .WithSummary("List workers (paged, filterable)")
            .Produces<PagedResult<WorkerDto>>();

        group.MapGet("/{id:guid}", async (Guid id, WorkerQueries queries, CancellationToken ct) =>
                (await queries.GetAsync(id, ct)).ToHttpResult(TypedResults.Ok))
            .WithName("GetWorker")
            .Produces<WorkerDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (CreateWorkerRequest request, WorkerCommands commands, CancellationToken ct) =>
                (await commands.CreateAsync(request, ct))
                    .ToHttpResult(worker => TypedResults.CreatedAtRoute(worker, "GetWorker", new { id = worker.Id })))
            .WithValidation<CreateWorkerRequest>()
            .WithSummary("Create a worker account with one or more roles")
            .Produces<WorkerDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (Guid id, UpdateWorkerRequest request, WorkerCommands commands, CancellationToken ct) =>
                (await commands.UpdateAsync(id, request, ct)).ToHttpResult(TypedResults.Ok))
            .WithValidation<UpdateWorkerRequest>()
            .Produces<WorkerDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/reset-password", async (Guid id, ResetPasswordRequest request, WorkerCommands commands, CancellationToken ct) =>
                (await commands.ResetPasswordAsync(id, request, ct)).ToHttpResult(TypedResults.NoContent))
            .WithValidation<ResetPasswordRequest>()
            .WithSummary("Set a new password and sign the worker out of all sessions")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/deactivate", async (Guid id, WorkerCommands commands, CancellationToken ct) =>
                (await commands.DeactivateAsync(id, ct)).ToHttpResult(TypedResults.NoContent))
            .WithSummary("Deactivate an account (instead of deleting it)")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/activate", async (Guid id, WorkerCommands commands, CancellationToken ct) =>
                (await commands.ActivateAsync(id, ct)).ToHttpResult(TypedResults.NoContent))
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
