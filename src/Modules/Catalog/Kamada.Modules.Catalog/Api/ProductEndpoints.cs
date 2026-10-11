using Kamada.Modules.Catalog.Application.Products;
using Kamada.Shared.Contracts.Auth;
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
            .WithTags("Products");

        // Policies of nested groups add up, so the endpoints sellers may call get their own group.
        var sales = group.MapGroup("/").RequireAuthorization(AuthorizationPolicies.SalesAccess);
        var admin = group.MapGroup("/").RequireAuthorization(AuthorizationPolicies.AdminOnly);

        sales.MapGet("/", async ([AsParameters] ProductListQuery query, ProductQueries queries, ICurrentUser user, CancellationToken ct) =>
                TypedResults.Ok(await queries.ListAsync(user.IsInRole(Roles.Admin) ? query : query with { IsActive = true }, ct)))
            .WithValidation<ProductListQuery>()
            .WithSummary("Search, filter (category incl. subcategories) and page products; sellers see active products only")
            .Produces<PagedResult<ProductDto>>();

        sales.MapGet("/lookup", async (HttpContext context, ProductQueries queries, CancellationToken ct) =>
                context.OkWithETag(await queries.LookupAsync(ct)))
            .WithSummary("All active products in one compact list (the phone app's offline copy); supports If-None-Match")
            .Produces<IReadOnlyList<ProductLookupDto>>()
            .Produces(StatusCodes.Status304NotModified);

        admin.MapGet("/{id:guid}", async (Guid id, ProductQueries queries, CancellationToken ct) =>
                (await queries.GetAsync(id, ct)).ToHttpResult(TypedResults.Ok))
            .WithName("GetProduct")
            .Produces<ProductDto>()
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("/", async (CreateProductRequest request, ProductCommands commands, CancellationToken ct) =>
                (await commands.CreateAsync(request, ct))
                    .ToHttpResult(product => TypedResults.CreatedAtRoute(product, "GetProduct", new { id = product.Id })))
            .WithValidation<CreateProductRequest>()
            .Produces<ProductDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapPut("/{id:guid}", async (Guid id, UpdateProductRequest request, ProductCommands commands, CancellationToken ct) =>
                (await commands.UpdateAsync(id, request, ct)).ToHttpResult(TypedResults.Ok))
            .WithValidation<UpdateProductRequest>()
            .Produces<ProductDto>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        admin.MapPut("/{id:guid}/stock", async (Guid id, SetStockRequest request, ProductCommands commands, CancellationToken ct) =>
                (await commands.SetStockAsync(id, request, ct)).ToHttpResult(TypedResults.NoContent))
            .WithValidation<SetStockRequest>()
            .WithSummary("Set absolute stock level")
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("/{id:guid}/deactivate", async (Guid id, ProductCommands commands, CancellationToken ct) =>
                (await commands.DeactivateAsync(id, ct)).ToHttpResult(TypedResults.NoContent))
            .WithSummary("Hide product from ordering (products are never deleted)")
            .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPost("/{id:guid}/activate", async (Guid id, ProductCommands commands, CancellationToken ct) =>
                (await commands.ActivateAsync(id, ct)).ToHttpResult(TypedResults.NoContent))
            .ProducesProblem(StatusCodes.Status404NotFound);

        return endpoints;
    }
}
