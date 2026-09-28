using Kamada.Modules.Catalog;
using Kamada.Modules.Customers;
using Kamada.Modules.Delivery;
using Kamada.Modules.Ordering;
using Kamada.Modules.Payments;
using Kamada.Modules.RouteOptimization;
using Kamada.Modules.Workforce;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Kamada")
    ?? throw new InvalidOperationException("Connection string 'Kamada' is not configured.");

builder.Services
    .AddWorkforceModule(builder.Configuration)
    .AddCatalogModule(builder.Configuration)
    .AddCustomersModule(builder.Configuration)
    .AddOrderingModule(builder.Configuration)
    .AddPaymentsModule(builder.Configuration)
    .AddRouteOptimizationModule(builder.Configuration)
    .AddDeliveryModule(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres");

var app = builder.Build();

app.MapHealthChecks("/health");

app.MapWorkforceEndpoints()
    .MapCatalogEndpoints()
    .MapCustomersEndpoints()
    .MapOrderingEndpoints()
    .MapPaymentsEndpoints()
    .MapRouteOptimizationEndpoints()
    .MapDeliveryEndpoints();

app.Run();
