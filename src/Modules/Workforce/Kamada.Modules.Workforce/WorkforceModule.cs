using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.Modules.Workforce;

/// <summary>
/// Entry point of the Workforce module: the only public type of the implementation assembly.
/// </summary>
public static class WorkforceModule
{
    public const string Name = "Workforce";

    public static IServiceCollection AddWorkforceModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapWorkforceEndpoints(this IEndpointRouteBuilder endpoints) => endpoints;
}
