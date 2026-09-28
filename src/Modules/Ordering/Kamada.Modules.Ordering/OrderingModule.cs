using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.Modules.Ordering;

/// <summary>
/// Entry point of the Ordering module: the only public type of the implementation assembly.
/// </summary>
public static class OrderingModule
{
    public const string Name = "Ordering";

    public static IServiceCollection AddOrderingModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapOrderingEndpoints(this IEndpointRouteBuilder endpoints) => endpoints;
}
