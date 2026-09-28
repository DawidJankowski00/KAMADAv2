using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.Modules.Customers;

/// <summary>
/// Entry point of the Customers module: the only public type of the implementation assembly.
/// </summary>
public static class CustomersModule
{
    public const string Name = "Customers";

    public static IServiceCollection AddCustomersModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapCustomersEndpoints(this IEndpointRouteBuilder endpoints) => endpoints;
}
