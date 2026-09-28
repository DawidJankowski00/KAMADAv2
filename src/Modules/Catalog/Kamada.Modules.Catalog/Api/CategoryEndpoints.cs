using Kamada.Modules.Catalog.Application.Categories;
using Kamada.Shared.Contracts.Catalog;
using Kamada.Shared.Infrastructure.Auth;
using Kamada.Shared.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kamada.Modules.Catalog.Api;

internal static class CategoryEndpoints
{
    public static IEndpointRouteBuilder MapCategoryEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/categories")
            .WithTags("Categories")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        group.MapGet("/", async (CategoryService categories, CancellationToken ct) =>
                TypedResults.Ok(await categories.GetTreeAsync(ct)))
            .WithSummary("Whole category tree")
            .Produces<IReadOnlyList<CategoryNodeDto>>();

        group.MapPost("/", async (CreateCategoryRequest request, CategoryService categories, CancellationToken ct) =>
                (await categories.CreateAsync(request, ct))
                    .ToHttpResult(category => TypedResults.Created($"/api/categories/{category.Id}", category)))
            .WithValidation<CreateCategoryRequest>()
            .Produces<CategoryRefDto>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPut("/{id:guid}", async (Guid id, UpdateCategoryRequest request, CategoryService categories, CancellationToken ct) =>
                (await categories.UpdateAsync(id, request, ct)).ToHttpResult(TypedResults.NoContent))
            .WithValidation<UpdateCategoryRequest>()
            .WithSummary("Rename and/or move a category")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapDelete("/{id:guid}", async (Guid id, CategoryService categories, CancellationToken ct) =>
                (await categories.DeleteAsync(id, ct)).ToHttpResult(TypedResults.NoContent))
            .WithSummary("Delete a leaf category (products are kept, only unassigned)")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
