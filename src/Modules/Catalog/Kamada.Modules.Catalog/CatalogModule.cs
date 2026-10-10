using FluentValidation;
using Kamada.Modules.Catalog.Api;
using Kamada.Modules.Catalog.Application.Abstractions;
using Kamada.Modules.Catalog.Application.Categories;
using Kamada.Modules.Catalog.Application.Inventory;
using Kamada.Modules.Catalog.Application.Lookup;
using Kamada.Modules.Catalog.Application.Products;
using Kamada.Modules.Catalog.Contracts;
using Kamada.Modules.Catalog.Infrastructure.Persistence;
using Kamada.Modules.Catalog.Infrastructure.Seeding;
using Kamada.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.Modules.Catalog;

/// <summary>
/// Entry point of the Catalog module (products, category tree, stock levels):
/// the only public type of the implementation assembly.
/// </summary>
public static class CatalogModule
{
    public const string Name = "Catalog";

    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<CatalogDbContext>(CatalogDbContext.Schema);
        services.AddScoped<ICatalogDbContext>(sp => sp.GetRequiredService<CatalogDbContext>());

        services.AddScoped<ProductQueries>();
        services.AddScoped<ProductCommands>();
        services.AddScoped<CategoryService>();
        services.AddScoped<ICatalogInventory, CatalogInventory>();
        services.AddScoped<ICatalogLookup, CatalogLookup>();
        services.AddScoped<IDataSeeder, CatalogSeeder>();
        services.AddValidatorsFromAssemblyContaining<ProductQueries>(includeInternalTypes: true);

        return services;
    }

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapProductEndpoints()
            .MapCategoryEndpoints();
}
