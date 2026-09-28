using FluentValidation;
using Kamada.Modules.Customers.Api;
using Kamada.Modules.Customers.Application;
using Kamada.Modules.Customers.Application.Abstractions;
using Kamada.Modules.Customers.Application.Clients;
using Kamada.Modules.Customers.Infrastructure.Persistence;
using Kamada.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.Modules.Customers;

/// <summary>
/// Entry point of the Customers module (clients and their geocoded addresses):
/// the only public type of the implementation assembly.
/// </summary>
public static class CustomersModule
{
    public const string Name = "Customers";

    public static IServiceCollection AddCustomersModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<CustomersDbContext>(CustomersDbContext.Schema);
        services.AddScoped<ICustomersDbContext>(sp => sp.GetRequiredService<CustomersDbContext>());

        services.AddScoped<AddressGeocoder>();
        services.AddScoped<ClientQueries>();
        services.AddScoped<ClientCommands>();
        services.AddValidatorsFromAssemblyContaining<ClientQueries>(includeInternalTypes: true);

        return services;
    }

    public static IEndpointRouteBuilder MapCustomersEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapClientEndpoints();
}
