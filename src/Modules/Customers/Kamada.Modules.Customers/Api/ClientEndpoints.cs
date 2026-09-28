using Kamada.Modules.Customers.Application.Clients;
using Kamada.Shared.Contracts.Common;
using Kamada.Shared.Contracts.Customers;
using Kamada.Shared.Infrastructure.Auth;
using Kamada.Shared.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kamada.Modules.Customers.Api;

internal static class ClientEndpoints
{
    public static IEndpointRouteBuilder MapClientEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/clients")
            .WithTags("Clients")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        group.MapGet("/", async ([AsParameters] ClientListQuery query, ClientQueries queries, CancellationToken ct) =>
                TypedResults.Ok(await queries.ListAsync(query, ct)))
            .WithValidation<ClientListQuery>()
            .Produces<PagedResult<ClientSummaryDto>>();

        group.MapGet("/{id:guid}", async (Guid id, ClientQueries queries, CancellationToken ct) =>
                (await queries.GetAsync(id, ct)).ToHttpResult(TypedResults.Ok))
            .WithName("GetClient")
            .Produces<ClientDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (CreateClientRequest request, ClientCommands commands, CancellationToken ct) =>
                (await commands.CreateAsync(request, ct))
                    .ToHttpResult(client => TypedResults.CreatedAtRoute(client, "GetClient", new { id = client.Id })))
            .WithValidation<CreateClientRequest>()
            .WithSummary("Create a client, optionally with addresses (geocoded automatically)")
            .Produces<ClientDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (Guid id, UpdateClientRequest request, ClientCommands commands, CancellationToken ct) =>
                (await commands.UpdateAsync(id, request, ct)).ToHttpResult(TypedResults.Ok))
            .WithValidation<UpdateClientRequest>()
            .Produces<ClientDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/deactivate", async (Guid id, ClientCommands commands, CancellationToken ct) =>
                (await commands.DeactivateAsync(id, ct)).ToHttpResult(TypedResults.NoContent))
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/activate", async (Guid id, ClientCommands commands, CancellationToken ct) =>
                (await commands.ActivateAsync(id, ct)).ToHttpResult(TypedResults.NoContent))
            .ProducesProblem(StatusCodes.Status404NotFound);

        var addresses = group.MapGroup("/{clientId:guid}/addresses").WithTags("Clients");

        addresses.MapPost("/", async (Guid clientId, AddressRequest request, ClientCommands commands, CancellationToken ct) =>
                (await commands.AddAddressAsync(clientId, request, ct))
                    .ToHttpResult(address => TypedResults.Created($"/api/clients/{clientId}/addresses/{address.Id}", address)))
            .WithValidation<AddressRequest>()
            .WithSummary("Add an address (geocoded automatically; saved without coordinates if not found)")
            .Produces<AddressDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status404NotFound);

        addresses.MapPut("/{addressId:guid}", async (Guid clientId, Guid addressId, AddressRequest request, ClientCommands commands, CancellationToken ct) =>
                (await commands.UpdateAddressAsync(clientId, addressId, request, ct)).ToHttpResult(TypedResults.Ok))
            .WithValidation<AddressRequest>()
            .WithSummary("Update an address (re-geocoded when the building changes)")
            .Produces<AddressDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        addresses.MapDelete("/{addressId:guid}", async (Guid clientId, Guid addressId, ClientCommands commands, CancellationToken ct) =>
                (await commands.ArchiveAddressAsync(clientId, addressId, ct)).ToHttpResult(TypedResults.NoContent))
            .WithSummary("Archive an address (kept for existing orders)")
            .ProducesProblem(StatusCodes.Status404NotFound);

        addresses.MapPut("/{addressId:guid}/location", async (Guid clientId, Guid addressId, SetAddressLocationRequest request, ClientCommands commands, CancellationToken ct) =>
                (await commands.SetAddressLocationAsync(clientId, addressId, request, ct)).ToHttpResult(TypedResults.Ok))
            .WithValidation<SetAddressLocationRequest>()
            .WithSummary("Set coordinates manually (pin on the map)")
            .Produces<AddressDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        addresses.MapPost("/{addressId:guid}/geocode", async (Guid clientId, Guid addressId, ClientCommands commands, CancellationToken ct) =>
                (await commands.GeocodeAddressAsync(clientId, addressId, ct)).ToHttpResult(TypedResults.Ok))
            .WithSummary("Retry automatic geocoding")
            .Produces<AddressDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
