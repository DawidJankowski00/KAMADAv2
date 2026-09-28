using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.RouteOptimization.Contracts;
using Kamada.Shared.Kernel.Geography;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.IntegrationTests.RouteOptimization;

/// <summary>The module's contracts resolve from the real host (matrix provider = Haversine in tests).</summary>
public sealed class RoutePlannerWiringTests(KamadaApiFactory factory)
{
    private static readonly GeoPoint Warehouse = new(53.12303, 18.00061);

    [Fact]
    public async Task OptimizeAsync_FromHostContainer_ReturnsApproximateOptimalPlan()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var planner = scope.ServiceProvider.GetRequiredService<IRoutePlanner>();
        var request = new RoutePlanRequest(
            Warehouse,
            Warehouse,
            [new GeoPoint(53.14, 18.02), new GeoPoint(53.13493, 17.98942), new GeoPoint(53.11, 18.05)]);

        var result = await planner.OptimizeAsync(request, [0, 1, 2], TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Optimized.MatrixProvider.ShouldBe("Haversine");
        result.Value.Optimized.IsApproximate.ShouldBeTrue();
        result.Value.Optimized.IsOptimal.ShouldBeTrue();
        result.Value.Optimized.TotalDurationSeconds.ShouldBeLessThanOrEqualTo(result.Value.Current!.TotalDurationSeconds);
    }

    [Fact]
    public void NavigationLinkBuilder_FromHostContainer_BuildsGoogleMapsLink()
    {
        var links = factory.Services.GetRequiredService<INavigationLinkBuilder>()
            .BuildRouteLinks([Warehouse, new GeoPoint(53.14, 18.02), Warehouse]);

        links.Single().Url.ShouldStartWith("https://www.google.com/maps/dir/?api=1&origin=53.12303%2C18.00061");
    }
}
