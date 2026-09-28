using System.Net;
using Kamada.Modules.RouteOptimization.Application;
using Kamada.Modules.RouteOptimization.Infrastructure.Matrix;
using Kamada.Shared.Kernel.Geography;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.RouteOptimization.UnitTests.Matrix;

public sealed class MatrixProviderTests
{
    private static readonly GeoPoint Warsaw = new(52.22977, 21.01178);
    private static readonly GeoPoint Krakow = new(50.06465, 19.94498);
    private static readonly GeoPoint Bydgoszcz = new(53.12350, 18.00762);

    private static IOptions<RouteOptimizationOptions> Options(string? googleKey = null) =>
        Microsoft.Extensions.Options.Options.Create(new RouteOptimizationOptions { Google = new GoogleRoutesOptions { ApiKey = googleKey } });

    [Fact]
    public void HaversineMeters_WarsawToKrakow_IsAbout252Km() =>
        StraightLineEstimate.HaversineMeters(Warsaw, Krakow).ShouldBe(252_000, 2_000);

    [Fact]
    public async Task Haversine_ThreePoints_SymmetricApproximateMatrix()
    {
        var matrix = await new HaversineMatrixProvider(Options()).GetMatrixAsync([Warsaw, Krakow, Bydgoszcz], CancellationToken.None);

        matrix.IsApproximate.ShouldBeTrue();
        matrix.Provider.ShouldBe(HaversineMatrixProvider.ProviderName);
        matrix.Duration(0, 0).ShouldBe(0);
        matrix.Distance(0, 1).ShouldBe(matrix.Distance(1, 0));
        matrix.Distance(0, 1).ShouldBe(StraightLineEstimate.HaversineMeters(Warsaw, Krakow) * 1.3, 1e-6);
        matrix.Duration(0, 1).ShouldBe(matrix.Distance(0, 1) / (30 / 3.6), 1e-6);
    }

    [Fact]
    public async Task Osrm_TableResponse_ParsesMatrixAndSendsLngLat()
    {
        var handler = new StubHttpHandler(_ => """
            {"code":"Ok","durations":[[0,120.5],[130,0]],"distances":[[0,1603.1],[1861.6,0]]}
            """);
        var provider = new OsrmMatrixProvider(handler.Client("http://osrm.test/"), Options());

        var matrix = await provider.GetMatrixAsync([new GeoPoint(53.1259, 18.0048), new GeoPoint(53.1266, 18.018)], CancellationToken.None);

        handler.Requests.Single().RequestUri!.ToString()
            .ShouldBe("http://osrm.test/table/v1/driving/18.0048,53.1259;18.018,53.1266?annotations=duration,distance");
        matrix.Provider.ShouldBe(OsrmMatrixProvider.ProviderName);
        matrix.IsApproximate.ShouldBeFalse();
        matrix.Duration(0, 1).ShouldBe(120.5);
        matrix.Duration(1, 0).ShouldBe(130);
        matrix.Distance(1, 0).ShouldBe(1861.6);
    }

    [Fact]
    public async Task Osrm_UnroutablePair_EstimatedAndFlaggedApproximate()
    {
        var handler = new StubHttpHandler(_ => """
            {"code":"Ok","durations":[[0,null],[130,0]],"distances":[[0,null],[1861.6,0]]}
            """);
        var provider = new OsrmMatrixProvider(handler.Client("http://osrm.test/"), Options());

        var matrix = await provider.GetMatrixAsync([Warsaw, Krakow], CancellationToken.None);

        matrix.IsApproximate.ShouldBeTrue();
        matrix.Distance(0, 1).ShouldBeGreaterThan(250_000);
        matrix.Duration(1, 0).ShouldBe(130);
    }

    [Fact]
    public async Task Osrm_ErrorCode_Throws()
    {
        var handler = new StubHttpHandler(_ => """{"code":"NoTable","message":"nope"}""");
        var provider = new OsrmMatrixProvider(handler.Client("http://osrm.test/"), Options());

        await Should.ThrowAsync<TravelMatrixProviderException>(() => provider.GetMatrixAsync([Warsaw, Krakow], CancellationToken.None));
    }

    [Fact]
    public async Task Google_ComputeRouteMatrix_ParsesElementsAndSendsKey()
    {
        // Proto3 JSON omits zero values: originIndex/destinationIndex 0 and distanceMeters 0 may be missing.
        var handler = new StubHttpHandler(_ => """
            [
              {"destinationIndex":1,"duration":"754s","distanceMeters":6100,"condition":"ROUTE_EXISTS"},
              {"originIndex":1,"duration":"801.5s","distanceMeters":6300,"condition":"ROUTE_EXISTS"},
              {"duration":"0s","condition":"ROUTE_EXISTS"},
              {"originIndex":1,"destinationIndex":1,"duration":"0s","condition":"ROUTE_EXISTS"}
            ]
            """);
        var provider = new GoogleRoutesMatrixProvider(handler.Client("http://google.test/"), Options("test-key"));

        var matrix = await provider.GetMatrixAsync([Bydgoszcz, new GeoPoint(53.13493, 17.98942)], CancellationToken.None);

        var request = handler.Requests.Single();
        request.RequestUri!.ToString().ShouldBe("http://google.test/distanceMatrix/v2:computeRouteMatrix");
        request.Headers.GetValues("X-Goog-Api-Key").ShouldBe(["test-key"]);
        request.Headers.GetValues("X-Goog-FieldMask").Single().ShouldContain("duration");
        handler.Bodies.Single().ShouldContain("\"latitude\":53.1235");
        matrix.Provider.ShouldBe(GoogleRoutesMatrixProvider.ProviderName);
        matrix.IsApproximate.ShouldBeFalse();
        matrix.Duration(0, 1).ShouldBe(754);
        matrix.Duration(1, 0).ShouldBe(801.5);
        matrix.Distance(1, 0).ShouldBe(6300);
    }

    [Fact]
    public async Task Google_RouteNotFound_EstimatedAndFlaggedApproximate()
    {
        var handler = new StubHttpHandler(_ => """
            [
              {"destinationIndex":1,"condition":"ROUTE_NOT_FOUND"},
              {"originIndex":1,"duration":"801s","distanceMeters":6300,"condition":"ROUTE_EXISTS"}
            ]
            """);
        var provider = new GoogleRoutesMatrixProvider(handler.Client("http://google.test/"), Options("test-key"));

        var matrix = await provider.GetMatrixAsync([Warsaw, Krakow], CancellationToken.None);

        matrix.IsApproximate.ShouldBeTrue();
        matrix.Distance(0, 1).ShouldBeGreaterThan(250_000);
    }

    [Fact]
    public async Task Google_MoreThan25Points_RequestsTiles()
    {
        var handler = new StubHttpHandler(_ => "[]");
        var provider = new GoogleRoutesMatrixProvider(handler.Client("http://google.test/"), Options("test-key"));
        var points = Enumerable.Range(0, 30).Select(i => new GeoPoint(53.1 + (i * 0.001), 18.0)).ToArray();

        await provider.GetMatrixAsync(points, CancellationToken.None);

        handler.Requests.Count.ShouldBe(4); // (25 + 5) origins × (25 + 5) destinations
    }

    [Fact]
    public async Task Google_HttpError_Throws()
    {
        var handler = new StubHttpHandler(_ => "{}", HttpStatusCode.Forbidden);
        var provider = new GoogleRoutesMatrixProvider(handler.Client("http://google.test/"), Options("bad-key"));

        await Should.ThrowAsync<HttpRequestException>(() => provider.GetMatrixAsync([Warsaw, Krakow], CancellationToken.None));
    }
}
