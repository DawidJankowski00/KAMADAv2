using Kamada.Modules.Delivery.Application.Locations;
using Kamada.Shared.Contracts.Delivery;
using Kamada.Shared.Infrastructure.Auth;
using Kamada.Shared.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kamada.Modules.Delivery.Api;

internal static class RouteLocationEndpoints
{
    public static IEndpointRouteBuilder MapRouteLocationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/route-locations")
            .WithTags("Route locations")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        group.MapGet("/", async (RouteLocationQueries queries, CancellationToken ct, bool includeInactive = false) =>
                TypedResults.Ok(await queries.ListAsync(includeInactive, ct)))
            .WithSummary("Start/end locations of delivery runs (warehouses, bases)")
            .Produces<IReadOnlyList<RouteLocationDto>>();

        group.MapGet("/{id:guid}", async (Guid id, RouteLocationQueries queries, CancellationToken ct) =>
                (await queries.GetAsync(id, ct)).ToHttpResult(TypedResults.Ok))
            .WithName("GetRouteLocation")
            .Produces<RouteLocationDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (RouteLocationRequest request, RouteLocationCommands commands, CancellationToken ct) =>
                (await commands.CreateAsync(request, ct))
                    .ToHttpResult(location => TypedResults.CreatedAtRoute(location, "GetRouteLocation", new { id = location.Id })))
            .WithValidation<RouteLocationRequest>()
            .WithSummary("Create a location (geocoded automatically; saved without coordinates if not found)")
            .Produces<RouteLocationDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (Guid id, RouteLocationRequest request, RouteLocationCommands commands, CancellationToken ct) =>
                (await commands.UpdateAsync(id, request, ct)).ToHttpResult(TypedResults.Ok))
            .WithValidation<RouteLocationRequest>()
            .WithSummary("Update a location (re-geocoded when the building changes)")
            .Produces<RouteLocationDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/location", async (Guid id, SetRouteLocationPinRequest request, RouteLocationCommands commands, CancellationToken ct) =>
                (await commands.SetPinAsync(id, request, ct)).ToHttpResult(TypedResults.Ok))
            .WithValidation<SetRouteLocationPinRequest>()
            .WithSummary("Set coordinates manually (pin on the map)")
            .Produces<RouteLocationDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/deactivate", async (Guid id, RouteLocationCommands commands, CancellationToken ct) =>
                (await commands.DeactivateAsync(id, ct)).ToHttpResult(TypedResults.NoContent))
            .WithSummary("Deactivate (existing runs keep it, new runs cannot use it)")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/activate", async (Guid id, RouteLocationCommands commands, CancellationToken ct) =>
                (await commands.ActivateAsync(id, ct)).ToHttpResult(TypedResults.NoContent))
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
