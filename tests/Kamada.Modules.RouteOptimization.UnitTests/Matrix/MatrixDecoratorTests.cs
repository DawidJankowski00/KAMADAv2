using Kamada.Modules.RouteOptimization.Application;
using Kamada.Modules.RouteOptimization.Application.Abstractions;
using Kamada.Modules.RouteOptimization.Domain;
using Kamada.Modules.RouteOptimization.Infrastructure.Matrix;
using Kamada.Shared.Kernel.Geography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly.Timeout;

namespace Kamada.Modules.RouteOptimization.UnitTests.Matrix;

public sealed class MatrixDecoratorTests
{
    private static readonly GeoPoint A = new(53.10000, 18.00000);
    private static readonly GeoPoint B = new(53.20000, 18.10000);
    private static readonly GeoPoint C = new(53.30000, 17.90000);

    private static readonly IOptions<RouteOptimizationOptions> Settings = Options.Create(new RouteOptimizationOptions());

    [Fact]
    public async Task Cache_SamePointsInDifferentOrder_CallsProviderOnceAndReindexes()
    {
        var inner = new RecordingProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new CachingTravelMatrixProvider(inner, cache, Settings);

        var first = await provider.GetMatrixAsync([A, B, C], CancellationToken.None);
        var second = await provider.GetMatrixAsync([C, A, B], CancellationToken.None);

        inner.Calls.Count.ShouldBe(1);
        second.Duration(0, 1).ShouldBe(first.Duration(2, 0)); // C → A
        second.Duration(1, 2).ShouldBe(first.Duration(0, 1)); // A → B
        second.Duration(2, 0).ShouldBe(first.Duration(1, 2)); // B → C
    }

    [Fact]
    public async Task Cache_CoordinatesDifferingBelowFiveDecimals_ShareEntry()
    {
        var inner = new RecordingProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new CachingTravelMatrixProvider(inner, cache, Settings);

        await provider.GetMatrixAsync([A, B], CancellationToken.None);
        await provider.GetMatrixAsync([new GeoPoint(53.100001, 18.000002), B], CancellationToken.None);

        inner.Calls.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Cache_DuplicatePoints_AskProviderForDistinctPointsWithZeroBetweenDuplicates()
    {
        var inner = new RecordingProvider();
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new CachingTravelMatrixProvider(inner, cache, Settings);

        var matrix = await provider.GetMatrixAsync([A, B, A], CancellationToken.None);

        inner.Calls.Single().Count.ShouldBe(2);
        matrix.Size.ShouldBe(3);
        matrix.Duration(0, 2).ShouldBe(0);
        matrix.Duration(2, 1).ShouldBe(matrix.Duration(0, 1));
    }

    [Fact]
    public async Task Cache_ApproximateMatrix_IsNotCached()
    {
        var inner = new RecordingProvider(approximate: true);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var provider = new CachingTravelMatrixProvider(inner, cache, Settings);

        await provider.GetMatrixAsync([A, B], CancellationToken.None);
        await provider.GetMatrixAsync([A, B], CancellationToken.None);

        inner.Calls.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(nameof(HttpRequestException))]
    [InlineData(nameof(TimeoutRejectedException))]
    [InlineData(nameof(TravelMatrixProviderException))]
    public async Task Fallback_ProviderFails_ReturnsApproximateEstimate(string exceptionType)
    {
        Exception failure = exceptionType switch
        {
            nameof(HttpRequestException) => new HttpRequestException("down"),
            nameof(TimeoutRejectedException) => new TimeoutRejectedException(),
            _ => new TravelMatrixProviderException("bad payload"),
        };
        var provider = new FallbackTravelMatrixProvider(
            new FailingProvider(failure), new HaversineMatrixProvider(Settings), NullLogger<FallbackTravelMatrixProvider>.Instance);

        var matrix = await provider.GetMatrixAsync([A, B], CancellationToken.None);

        matrix.Provider.ShouldBe(HaversineMatrixProvider.ProviderName);
        matrix.IsApproximate.ShouldBeTrue();
    }

    [Fact]
    public async Task Fallback_CallerCancelled_PropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var provider = new FallbackTravelMatrixProvider(
            new FailingProvider(new OperationCanceledException(cts.Token)),
            new HaversineMatrixProvider(Settings),
            NullLogger<FallbackTravelMatrixProvider>.Instance);

        await Should.ThrowAsync<OperationCanceledException>(() => provider.GetMatrixAsync([A, B], cts.Token));
    }

    [Fact]
    public async Task Fallback_ProgrammingError_IsNotSwallowed()
    {
        var provider = new FallbackTravelMatrixProvider(
            new FailingProvider(new InvalidOperationException("bug")),
            new HaversineMatrixProvider(Settings),
            NullLogger<FallbackTravelMatrixProvider>.Instance);

        await Should.ThrowAsync<InvalidOperationException>(() => provider.GetMatrixAsync([A, B], CancellationToken.None));
    }

    /// <summary>Asymmetric fake: duration i → j = 100·(index of i) + (index of j) + 1, by position in the request.</summary>
    private sealed class RecordingProvider(bool approximate = false) : ITravelTimeMatrixProvider
    {
        public List<IReadOnlyList<GeoPoint>> Calls { get; } = [];

        public Task<TravelMatrix> GetMatrixAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken)
        {
            Calls.Add(points);
            var n = points.Count;
            var d = new double[n, n];
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < n; j++)
                {
                    d[i, j] = i == j ? 0 : (100 * i) + j + 1;
                }
            }

            return Task.FromResult(new TravelMatrix(d, (double[,])d.Clone(), "Recording", approximate));
        }
    }

    private sealed class FailingProvider(Exception failure) : ITravelTimeMatrixProvider
    {
        public Task<TravelMatrix> GetMatrixAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken) =>
            Task.FromException<TravelMatrix>(failure);
    }
}
