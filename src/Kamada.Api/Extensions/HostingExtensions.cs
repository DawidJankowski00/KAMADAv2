using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Kamada.Shared.Infrastructure.Auth;
using Kamada.Shared.Infrastructure.Http;
using Microsoft.Net.Http.Headers;
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
            .AllowAnyMethod()
            // The phone app reads the tag of its offline lookup data to send it back in If-None-Match.
            .WithExposedHeaders(HeaderNames.ETag)));
    }

    /// <summary>
    /// Fixed windows: per client IP on authentication endpoints, per signed-in user on AI inference.
    /// Limits are configurable (<c>RateLimiting:&lt;Policy&gt;</c>) so tests can raise them.
    /// </summary>
    public static IServiceCollection AddKamadaRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var login = FixedWindow(configuration, "Login", permitLimit: 10, TimeSpan.FromMinutes(1));
        var assistant = FixedWindow(configuration, "Assistant", permitLimit: 20, TimeSpan.FromMinutes(1));

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }

                var policy = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
                await TypedResults.Problem(
                        statusCode: StatusCodes.Status429TooManyRequests,
                        title: "Too many requests",
                        detail: policy == RateLimitPolicies.Login
                            ? "Too many sign-in attempts. Try again later."
                            : "Too many requests. Try again later.")
                    .ExecuteAsync(context.HttpContext);
            };

            options.AddPolicy(RateLimitPolicies.Login, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => login));

            options.AddPolicy(RateLimitPolicies.Assistant, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.User.FindFirst(KamadaClaimTypes.Subject)?.Value
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown",
                _ => assistant));
        });
    }

    private static FixedWindowRateLimiterOptions FixedWindow(IConfiguration configuration, string policy, int permitLimit, TimeSpan window) => new()
    {
        PermitLimit = configuration.GetValue($"RateLimiting:{policy}:PermitLimit", permitLimit),
        Window = configuration.GetValue($"RateLimiting:{policy}:Window", window),
        QueueLimit = 0,
    };
}
