using Kamada.Modules.Catalog.Application.Products;
using Kamada.Shared.Contracts.Catalog;
using Kamada.Shared.Contracts.Common;
using Kamada.Shared.Infrastructure.Auth;
using Kamada.Shared.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kamada.Modules.Catalog.Api;

internal static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/products")
            .WithTags("Products")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        group.MapGet("/", async ([AsParameters] ProductListQuery query, ProductQueries queries, CancellationToken ct) =>
                TypedResults.Ok(await queries.ListAsync(query, ct)))
            .WithValidation<ProductListQuery>()
            .WithSummary("Search, filter (category incl. subcategories) and page products")
            .Produces<PagedResult<ProductDto>>();

        group.MapGet("/{id:guid}", async (Guid id, ProductQueries queries, CancellationToken ct) =>
                (await queries.GetAsync(id, ct)).ToHttpResult(TypedResults.Ok))
            .WithName("GetProduct")
            .Produces<ProductDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", async (CreateProductRequest request, ProductCommands commands, CancellationToken ct) =>
                (await commands.CreateAsync(request, ct))
                    .ToHttpResult(product => TypedResults.CreatedAtRoute(product, "GetProduct", new { id = product.Id })))
            .WithValidation<CreateProductRequest>()
            .Produces<ProductDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (Guid id, UpdateProductRequest request, ProductCommands commands, CancellationToken ct) =>
                (await commands.UpdateAsync(id, request, ct)).ToHttpResult(TypedResults.Ok))
            .WithValidation<UpdateProductRequest>()
            .Produces<ProductDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}/stock", async (Guid id, SetStockRequest request, ProductCommands commands, CancellationToken ct) =>
                (await commands.SetStockAsync(id, request, ct)).ToHttpResult(TypedResults.NoContent))
            .WithValidation<SetStockRequest>()
            .WithSummary("Set absolute stock level")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/deactivate", async (Guid id, ProductCommands commands, CancellationToken ct) =>
                (await commands.DeactivateAsync(id, ct)).ToHttpResult(TypedResults.NoContent))
            .WithSummary("Hide product from ordering (products are never deleted)")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id:guid}/activate", async (Guid id, ProductCommands commands, CancellationToken ct) =>
                (await commands.ActivateAsync(id, ct)).ToHttpResult(TypedResults.NoContent))
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
