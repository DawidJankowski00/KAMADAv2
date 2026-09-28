using Kamada.Api.Extensions;
using Kamada.Modules.Catalog;
using Kamada.Modules.Customers;
using Kamada.Modules.Delivery;
using Kamada.Modules.Ordering;
using Kamada.Modules.Payments;
using Kamada.Modules.RouteOptimization;
using Kamada.Modules.Workforce;
using Kamada.Shared.Infrastructure.Auth;
using Kamada.Shared.Infrastructure.Persistence;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(TimeProvider.System);
builder.Services
    .AddKamadaLogging()
    .AddKamadaHttp()
    .AddKamadaOpenApi()
    .AddKamadaCors(builder.Configuration)
    .AddKamadaRateLimiting(builder.Configuration)
    .AddKamadaAuthentication(builder.Configuration)
    .AddKamadaPersistence(builder.Configuration);

builder.Services
    .AddWorkforceModule(builder.Configuration)
    .AddCatalogModule(builder.Configuration)
    .AddCustomersModule(builder.Configuration)
    .AddOrderingModule(builder.Configuration)
    .AddPaymentsModule(builder.Configuration)
    .AddRouteOptimizationModule(builder.Configuration)
    .AddDeliveryModule(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddNpgSql(name: "postgres");

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSerilogRequestLogging();
app.UseCors(HostingExtensions.FrontendsCorsPolicy);
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

if (!app.Environment.IsProduction())
{
    app.MapKamadaApiReference();
}

app.MapHealthChecks("/health").AllowAnonymous();

app.MapWorkforceEndpoints()
    .MapCatalogEndpoints()
    .MapCustomersEndpoints()
    .MapOrderingEndpoints()
    .MapPaymentsEndpoints()
    .MapRouteOptimizationEndpoints()
    .MapDeliveryEndpoints();

await app.Services.InitializeDatabasesAsync(app.Lifetime.ApplicationStopping);

await app.RunAsync();
