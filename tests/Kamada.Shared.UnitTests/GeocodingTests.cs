using Kamada.Shared.Infrastructure.Geocoding;
using Kamada.Shared.Infrastructure.Http;
using Kamada.Shared.Kernel.Geography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Kamada.Shared.UnitTests;

public sealed class GeocodingTests
{
    private static readonly GeocodingRequest Request = new("Gdańska", "10", "Bydgoszcz", "85-005", "Polska");

    private static NominatimGeocodingService Nominatim(StubHttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://nominatim.test/") });

    [Fact]
    public async Task Nominatim_ParsesFirstResultAndSendsStructuredQuery()
    {
        var handler = StubHttpMessageHandler.Json("""
            [{ "lat": "53.1287650", "lon": "18.0060450", "display_name": "10, Gdańska, Bydgoszcz" }]
            """);

        var result = await Nominatim(handler).GeocodeAsync(Request, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Location.ShouldBe(new GeoPoint(53.128765, 18.006045));
        result.Provider.ShouldBe("Nominatim");
        var query = Uri.UnescapeDataString(handler.Requests.Single().RequestUri!.Query);
        query.ShouldContain("street=10 Gdańska");
        query.ShouldContain("postalcode=85-005");
        query.ShouldContain("format=jsonv2");
    }

    [Fact]
    public async Task Nominatim_ReturnsAddressDetailsAndSkipsEmptyQueryParts()
    {
        var handler = StubHttpMessageHandler.Json("""
            [{ "lat": "54.38", "lon": "18.60", "display_name": "15, Aleja Grunwaldzka, Gdańsk",
               "address": { "road": "Aleja Grunwaldzka", "house_number": "15", "postcode": "80-236", "city": "Gdańsk" } }]
            """);

        var result = await Nominatim(handler).GeocodeAsync(
            new GeocodingRequest("Grunwaldzka", "15", "Gdańsk", string.Empty, "Polska"),
            TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.Address.ShouldBe(new GeocodedAddress("Aleja Grunwaldzka", "15", "80-236", "Gdańsk"));
        var query = Uri.UnescapeDataString(handler.Requests.Single().RequestUri!.Query);
        query.ShouldContain("addressdetails=1");
        query.ShouldNotContain("postalcode");
    }

    [Fact]
    public void Request_ToQueryString_SkipsEmptyParts() =>
        new GeocodingRequest("Grunwaldzka", "15", "Gdańsk", " ", "Polska").ToQueryString().ShouldBe("Grunwaldzka 15, Gdańsk, Polska");

    [Fact]
    public async Task Nominatim_NoResults_ReturnsNull()
    {
        var result = await Nominatim(StubHttpMessageHandler.Json("[]")).GeocodeAsync(Request, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Caching_SecondLookupOfSameAddress_DoesNotCallProvider()
    {
        var inner = new CountingGeocoder(new GeocodingResult(new GeoPoint(53, 18), "Fake", null));
        var service = Cached(inner);

        await service.GeocodeAsync(Request, TestContext.Current.CancellationToken);
        var second = await service.GeocodeAsync(Request with { City = " bydgoszcz " }, TestContext.Current.CancellationToken);

        second.ShouldNotBeNull();
        inner.Calls.ShouldBe(1);
    }

    [Fact]
    public async Task Caching_ProviderFailure_ReturnsNullInsteadOfThrowing()
    {
        var service = Cached(new ThrowingGeocoder());

        var result = await service.GeocodeAsync(Request, TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task Throttle_SecondRequestWaitsForMinimumDelay()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        using var throttle = new RequestThrottle(TimeSpan.FromSeconds(1), clock);
        var ct = TestContext.Current.CancellationToken;

        await throttle.WaitAsync(ct);
        var second = throttle.WaitAsync(ct);

        second.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromMilliseconds(999));
        second.IsCompleted.ShouldBeFalse();
        clock.Advance(TimeSpan.FromMilliseconds(1));
        await second.WaitAsync(TimeSpan.FromSeconds(5), ct);
    }

    private static CachingGeocodingService Cached(IGeocodingService inner) => new(
        inner,
        new MemoryCache(new MemoryCacheOptions()),
        MsOptions.Create(new GeocodingOptions()),
        NullLogger<CachingGeocodingService>.Instance);

    private sealed class CountingGeocoder(GeocodingResult? result) : IGeocodingService
    {
        public int Calls { get; private set; }

        public Task<GeocodingResult?> GeocodeAsync(GeocodingRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    private sealed class ThrowingGeocoder : IGeocodingService
    {
        public Task<GeocodingResult?> GeocodeAsync(GeocodingRequest request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Service unavailable");
    }
}
