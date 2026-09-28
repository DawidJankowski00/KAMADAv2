using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.Modules.Catalog;

/// <summary>
/// Entry point of the Catalog module: the only public type of the implementation assembly.
/// </summary>
public static class CatalogModule
{
    public const string Name = "Catalog";

    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapCatalogEndpoints(this IEndpointRouteBuilder endpoints) => endpoints;
}
