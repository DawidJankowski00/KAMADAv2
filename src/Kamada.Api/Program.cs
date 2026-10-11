using Kamada.Api.Extensions;
using Kamada.Modules.Assistant;
using Kamada.Modules.Catalog;
using Kamada.Modules.Customers;
using Kamada.Modules.Delivery;
using Kamada.Modules.Ordering;
using Kamada.Modules.Payments;
using Kamada.Modules.RouteOptimization;
using Kamada.Modules.Workforce;
using Kamada.Shared.Infrastructure.Auth;
using Kamada.Shared.Infrastructure.Geocoding;
using Kamada.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services
    .AddKamadaLogging()
    .AddKamadaHttp()
    .AddKamadaOpenApi()
    .AddKamadaCors(builder.Configuration)
    .AddKamadaRateLimiting(builder.Configuration)
    .AddKamadaAuthentication(builder.Configuration)
    .AddKamadaPersistence(builder.Configuration)
    .AddKamadaGeocoding(builder.Configuration);

builder.Services
    .AddWorkforceModule(builder.Configuration)
    .AddCatalogModule(builder.Configuration)
    .AddCustomersModule(builder.Configuration)
    .AddOrderingModule(builder.Configuration)
    .AddPaymentsModule(builder.Configuration)
    .AddRouteOptimizationModule(builder.Configuration)
    .AddDeliveryModule(builder.Configuration)
    .AddAssistantModule(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddNpgSql(name: "postgres", tags: [HealthTags.Ping]);

var app = builder.Build();

// Request logging wraps the exception handler, so it records the final status code (not 500 for handled errors).
// Phone pings of /health/ping are not worth an information line each.
app.UseSerilogRequestLogging(options => options.GetLevel = (context, _, exception) =>
    exception is not null || context.Response.StatusCode >= 500 ? LogEventLevel.Error
    : context.Request.Path.StartsWithSegments("/health/ping") ? LogEventLevel.Verbose
    : LogEventLevel.Information);
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(HostingExtensions.FrontendsCorsPolicy);
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (!app.Environment.IsProduction())
{
    app.MapKamadaApiReference();
}

app.MapHealthChecks("/health").AllowAnonymous();

// Polled by the phone app to judge the connection: only the database, so frequent pings never wake the AI model or OSRM.
app.MapHealthChecks("/health/ping", new HealthCheckOptions { Predicate = check => check.Tags.Contains(HealthTags.Ping) })
    .AllowAnonymous();

app.MapWorkforceEndpoints()
    .MapCatalogEndpoints()
    .MapCustomersEndpoints()
    .MapOrderingEndpoints()
    .MapPaymentsEndpoints()
    .MapRouteOptimizationEndpoints()
    .MapDeliveryEndpoints()
    .MapAssistantEndpoints();

await app.Services.InitializeDatabasesAsync(app.Lifetime.ApplicationStopping);

await app.RunAsync();
