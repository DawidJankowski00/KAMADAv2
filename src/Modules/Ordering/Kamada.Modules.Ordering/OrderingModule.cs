using FluentValidation;
using Kamada.Modules.Ordering.Api;
using Kamada.Modules.Ordering.Application;
using Kamada.Modules.Ordering.Application.Abstractions;
using Kamada.Modules.Ordering.Application.Orders;
using Kamada.Modules.Ordering.Contracts;
using Kamada.Modules.Ordering.Infrastructure.Persistence;
using Kamada.Modules.Ordering.Infrastructure.Seeding;
using Kamada.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.Modules.Ordering;

/// <summary>
/// Entry point of the Ordering module (orders, order lines, order lifecycle):
/// the only public type of the implementation assembly.
/// </summary>
public static class OrderingModule
{
    public const string Name = "Ordering";

    public static IServiceCollection AddOrderingModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<OrderingDbContext>(OrderingDbContext.Schema);
        services.AddScoped<IOrderingDbContext>(sp => sp.GetRequiredService<OrderingDbContext>());

        services.AddScoped<PlaceOrderHandler>();
        services.AddScoped<OrderQueries>();
        services.AddScoped<OrderCommands>();
        services.AddScoped<IOrderPayments, OrderPayments>();
        services.AddScoped<IOrderFulfillment, OrderFulfillment>();
        services.AddScoped<IDataSeeder, OrderingSeeder>();
        services.AddScoped<IDataSeeder, OrderPackingSeeder>();
        services.AddValidatorsFromAssemblyContaining<OrderQueries>(includeInternalTypes: true);

        return services;
    }

    public static IEndpointRouteBuilder MapOrderingEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapOrderEndpoints();
}
