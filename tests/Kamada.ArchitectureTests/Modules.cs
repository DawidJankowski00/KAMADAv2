using System.Reflection;
using Kamada.Modules.Catalog;
using Kamada.Modules.Customers;
using Kamada.Modules.Delivery;
using Kamada.Modules.Ordering;
using Kamada.Modules.Payments;
using Kamada.Modules.RouteOptimization;
using Kamada.Modules.Workforce;

namespace Kamada.ArchitectureTests;

/// <summary>
/// Single source of truth for the list of modules checked by architecture tests.
/// </summary>
internal static class Modules
{
    public static readonly IReadOnlyList<ModuleInfo> All =
    [
        new(CatalogModule.Name, typeof(CatalogModule).Assembly),
        new(CustomersModule.Name, typeof(CustomersModule).Assembly),
        new(OrderingModule.Name, typeof(OrderingModule).Assembly),
        new(PaymentsModule.Name, typeof(PaymentsModule).Assembly),
        new(WorkforceModule.Name, typeof(WorkforceModule).Assembly),
        new(DeliveryModule.Name, typeof(DeliveryModule).Assembly),
        new(RouteOptimizationModule.Name, typeof(RouteOptimizationModule).Assembly),
    ];

    public static TheoryData<string> Names => new(All.Select(m => m.Name));

    public static ModuleInfo Get(string name) => All.Single(m => m.Name == name);
}

internal sealed record ModuleInfo(string Name, Assembly Assembly)
{
    public string RootNamespace => $"Kamada.Modules.{Name}";

    public IEnumerable<string> InternalNamespaces =>
        new[] { "Domain", "Application", "Infrastructure", "Api" }.Select(layer => $"{RootNamespace}.{layer}");
}
