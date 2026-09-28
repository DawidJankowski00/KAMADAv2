using FluentValidation;
using Kamada.Modules.Payments.Api;
using Kamada.Modules.Payments.Application;
using Kamada.Modules.Payments.Application.Abstractions;
using Kamada.Modules.Payments.Infrastructure;
using Kamada.Modules.Payments.Infrastructure.Persistence;
using Kamada.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.Modules.Payments;

/// <summary>
/// Entry point of the Payments module (payments with a simulated provider):
/// the only public type of the implementation assembly.
/// </summary>
public static class PaymentsModule
{
    public const string Name = "Payments";

    public static IServiceCollection AddPaymentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<PaymentsDbContext>(PaymentsDbContext.Schema);
        services.AddScoped<IPaymentsDbContext>(sp => sp.GetRequiredService<PaymentsDbContext>());

        services.AddSingleton<IPaymentGateway, SimulatedPaymentGateway>();
        services.AddScoped<PaymentService>();
        services.AddValidatorsFromAssemblyContaining<PaymentService>(includeInternalTypes: true);

        return services;
    }

    public static IEndpointRouteBuilder MapPaymentsEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapPaymentEndpoints();
}
