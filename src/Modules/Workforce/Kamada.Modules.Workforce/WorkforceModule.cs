using FluentValidation;
using Kamada.Modules.Workforce.Api;
using Kamada.Modules.Workforce.Application.Abstractions;
using Kamada.Modules.Workforce.Application.Auth;
using Kamada.Modules.Workforce.Application.Workers;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Modules.Workforce.Infrastructure.Auth;
using Kamada.Modules.Workforce.Infrastructure.Persistence;
using Kamada.Modules.Workforce.Infrastructure.Seeding;
using Kamada.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.Modules.Workforce;

/// <summary>
/// Entry point of the Workforce module (workers, authentication, roles):
/// the only public type of the implementation assembly.
/// </summary>
public static class WorkforceModule
{
    public const string Name = "Workforce";

    public static IServiceCollection AddWorkforceModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddModuleDbContext<WorkforceDbContext>(WorkforceDbContext.Schema);
        services.AddScoped<IWorkforceDbContext>(sp => sp.GetRequiredService<WorkforceDbContext>());

        services.AddSingleton<IPasswordHasher<Worker>, PasswordHasher<Worker>>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddScoped<AuthSessionFactory>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<RefreshTokenHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<WorkerQueries>();
        services.AddScoped<WorkerCommands>();

        services.AddValidatorsFromAssemblyContaining<WorkerQueries>(includeInternalTypes: true);
        services.AddScoped<IDataSeeder, WorkforceSeeder>();

        return services;
    }

    public static IEndpointRouteBuilder MapWorkforceEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapAuthEndpoints()
            .MapWorkerEndpoints();
}
