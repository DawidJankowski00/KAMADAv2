using FluentValidation;
using Kamada.Modules.Delivery.Api;
using Kamada.Modules.Delivery.Application;
using Kamada.Modules.Delivery.Application.Abstractions;
using Kamada.Modules.Delivery.Application.Locations;
using Kamada.Modules.Delivery.Application.Runs;
using Kamada.Modules.Delivery.Infrastructure.Persistence;
using Kamada.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.Modules.Delivery;

/// <summary>
/// Entry point of the Delivery module (route locations, delivery runs, stops, courier workflow):
/// the only public type of the implementation assembly.
/// </summary>
public static class DeliveryModule
{
    public const string Name = "Delivery";

    public static IServiceCollection AddDeliveryModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<DeliveryOptions>()
            .Bind(configuration.GetSection(DeliveryOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddModuleDbContext<DeliveryDbContext>(DeliveryDbContext.Schema);
        services.AddScoped<IDeliveryDbContext>(sp => sp.GetRequiredService<DeliveryDbContext>());

        services.AddScoped<RouteLocationQueries>();
        services.AddScoped<RouteLocationCommands>();
        services.AddScoped<StopLocationResolver>();
        services.AddScoped<DeliveryRunReader>();
        services.AddScoped<DeliveryRunQueries>();
        services.AddScoped<DeliveryRunCommands>();
        services.AddScoped<RoutePlanningHandler>();
        services.AddScoped<CourierRunCommands>();
        services.AddValidatorsFromAssemblyContaining<DeliveryRunQueries>(includeInternalTypes: true);

        return services;
    }

    public static IEndpointRouteBuilder MapDeliveryEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapRouteLocationEndpoints()
            .MapDeliveryRunEndpoints()
            .MapCourierRunEndpoints();
}
