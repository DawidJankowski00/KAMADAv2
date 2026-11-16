using FluentValidation;
using Kamada.Modules.Workforce.Api;
using Kamada.Modules.Workforce.Application;
using Kamada.Modules.Workforce.Application.Abstractions;
using Kamada.Modules.Workforce.Application.Auth;
using Kamada.Modules.Workforce.Application.Workers;
using Kamada.Modules.Workforce.Contracts;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Modules.Workforce.Infrastructure.Auth;
using Kamada.Modules.Workforce.Infrastructure.Persistence;
using Kamada.Modules.Workforce.Infrastructure.Seeding;
using Kamada.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
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
        services.AddDataProtection()
            .SetApplicationName("kamada")
            .PersistKeysToDbContext<WorkforceDbContext>();
        services.AddScoped<IWorkforceDbContext>(sp => sp.GetRequiredService<WorkforceDbContext>());

        services.AddSingleton<IPasswordHasher<Worker>, PasswordHasher<Worker>>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddScoped<AuthSessionFactory>();
        services.AddScoped<LoginHandler>();
        services.AddScoped<ChangePasswordHandler>();
        services.AddScoped<ForgotPasswordHandler>();
        services.AddScoped<ResetPasswordHandler>();
        services.AddOptions<AccountLockoutOptions>()
            .Bind(configuration.GetSection(AccountLockoutOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<PasswordResetOptions>()
            .Bind(configuration.GetSection(PasswordResetOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<RefreshTokenHandler>();
        services.AddScoped<LogoutHandler>();
        services.AddScoped<AccountSessions>();
        services.AddSingleton<ITwoFactorService, TotpTwoFactorService>();
        services.AddSingleton<TwoFactorPolicy>();
        services.AddSingleton<PasswordPolicy>();
        services.AddOptions<PasswordPolicyOptions>().Bind(configuration.GetSection(PasswordPolicyOptions.SectionName));
        services.AddOptions<TwoFactorPolicyOptions>().Bind(configuration.GetSection(TwoFactorPolicyOptions.SectionName));
        services.AddScoped<TwoFactorCodes>();
        services.AddScoped<TwoFactorSetup>();
        services.AddScoped<TwoFactorLoginHandler>();
        services.AddScoped<WorkerQueries>();
        services.AddScoped<WorkerCommands>();
        services.AddScoped<ICourierDirectory, CourierDirectory>();
        services.AddScoped<IWorkerDirectory, WorkerDirectory>();

        services.AddValidatorsFromAssemblyContaining<WorkerQueries>(includeInternalTypes: true);
        services.AddScoped<IDataSeeder, WorkforceSeeder>();

        return services;
    }

    public static IEndpointRouteBuilder MapWorkforceEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapAuthEndpoints()
            .MapWorkerEndpoints();
}
