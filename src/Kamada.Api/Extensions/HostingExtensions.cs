using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Kamada.Shared.Infrastructure.Http;
using Serilog;

namespace Kamada.Api.Extensions;

/// <summary>
/// Host-level (cross-cutting) configuration. Module-specific registrations live in each module.
/// </summary>
internal static class HostingExtensions
{
    public const string FrontendsCorsPolicy = "Frontends";

    public static IServiceCollection AddKamadaLogging(this IServiceCollection services) =>
        services.AddSerilog((serviceProvider, logger) => logger
            .ReadFrom.Configuration(serviceProvider.GetRequiredService<IConfiguration>())
            .ReadFrom.Services(serviceProvider)
            .Enrich.FromLogContext()
            .Enrich.WithProperty("Application", "Kamada.Api"));

    public static IServiceCollection AddKamadaHttp(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
            context.ProblemDetails.Extensions.TryAdd("traceId", context.HttpContext.TraceIdentifier));
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        return services;
    }

    public static IServiceCollection AddKamadaCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        return services.AddCors(options => options.AddPolicy(FrontendsCorsPolicy, policy => policy
            .WithOrigins(origins)
            .AllowAnyHeader()
            .AllowAnyMethod()));
    }

    /// <summary>
    /// Fixed window per client IP on authentication endpoints. Limits are configurable so tests can raise them.
    /// </summary>
    public static IServiceCollection AddKamadaRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue("RateLimiting:Login:PermitLimit", 10);
        var window = configuration.GetValue("RateLimiting:Login:Window", TimeSpan.FromMinutes(1));

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                await TypedResults.Problem(
                        statusCode: StatusCodes.Status429TooManyRequests,
                        title: "Too many requests",
                        detail: "Too many sign-in attempts. Try again later.")
                    .ExecuteAsync(context.HttpContext);
            };

            options.AddPolicy(RateLimitPolicies.Login, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = window,
                    QueueLimit = 0,
                }));
        });
    }
}
