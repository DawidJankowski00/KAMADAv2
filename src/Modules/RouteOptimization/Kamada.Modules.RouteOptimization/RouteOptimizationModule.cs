using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.Modules.RouteOptimization;

/// <summary>
/// Entry point of the RouteOptimization module: the only public type of the implementation assembly.
/// </summary>
public static class RouteOptimizationModule
{
    public const string Name = "RouteOptimization";

    public static IServiceCollection AddRouteOptimizationModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapRouteOptimizationEndpoints(this IEndpointRouteBuilder endpoints) => endpoints;
}
