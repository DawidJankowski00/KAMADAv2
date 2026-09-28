using Kamada.Modules.RouteOptimization.Infrastructure.GoogleMaps;
using Kamada.Shared.Kernel.Geography;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.RouteOptimization.UnitTests.GoogleMaps;

public sealed class GoogleMapsUrlBuilderTests
{
    private static GoogleMapsUrlBuilder Builder(int maxWaypoints = 9) =>
        new(Options.Create(new GoogleMapsOptions { MaxWaypointsPerLink = maxWaypoints }));

    private static GeoPoint[] Route(int count) =>
        [.. Enumerable.Range(0, count).Select(i => new GeoPoint(53.1 + (i / 1000.0), 18.0 + (i / 1000.0)))];

    [Fact]
    public void BuildRouteLinks_OriginAndDestinationOnly_NoWaypointsParameter()
    {
        var link = Builder().BuildRouteLinks([new GeoPoint(53.12303, 18.00061), new GeoPoint(53.13493, 17.98942)]).Single();

        link.Url.ShouldBe(
            "https://www.google.com/maps/dir/?api=1&origin=53.12303%2C18.00061&destination=53.13493%2C17.98942&travelmode=driving");
        (link.Part, link.TotalParts, link.FromIndex, link.ToIndex).ShouldBe((1, 1, 0, 1));
    }

    [Fact]
    public void BuildRouteLinks_Waypoints_AreCoordinatesSeparatedByEncodedPipe()
    {
        GeoPoint[] points = [new(53.1, 18.0), new(53.2, 18.1), new(53.3, 18.2), new(53.4, 18.3)];

        var link = Builder().BuildRouteLinks(points).Single();

        link.Url.ShouldBe(
            "https://www.google.com/maps/dir/?api=1&origin=53.1%2C18&destination=53.4%2C18.3"
            + "&waypoints=53.2%2C18.1%7C53.3%2C18.2&travelmode=driving");
        link.Url.ShouldNotContain("|");
    }

    [Fact]
    public void BuildRouteLinks_ExactlyMaxWaypoints_SingleLink()
    {
        var links = Builder(maxWaypoints: 9).BuildRouteLinks(Route(11)); // origin + 9 + destination

        links.Count.ShouldBe(1);
        WaypointCount(links[0].Url).ShouldBe(9);
    }

    [Fact]
    public void BuildRouteLinks_OneMoreThanFits_SplitsIntoTwoChainedParts()
    {
        var points = Route(12);

        var links = Builder(maxWaypoints: 9).BuildRouteLinks(points);

        links.Count.ShouldBe(2);
        links.ShouldAllBe(l => l.TotalParts == 2);
        links[0].FromIndex.ShouldBe(0);
        links[0].ToIndex.ShouldBe(links[1].FromIndex);
        links[1].ToIndex.ShouldBe(11);
        links[0].Url.ShouldContain($"destination={Uri.EscapeDataString(points[links[0].ToIndex].ToString())}");
        links[1].Url.ShouldContain($"origin={Uri.EscapeDataString(points[links[1].FromIndex].ToString())}");
    }

    [Theory]
    [InlineData(2, 9)]
    [InlineData(25, 9)]
    [InlineData(25, 3)]
    [InlineData(62, 9)]
    [InlineData(30, 1)]
    public void BuildRouteLinks_AnyLength_PartsChainCoverEveryPointAndRespectLimit(int pointCount, int maxWaypoints)
    {
        var links = Builder(maxWaypoints).BuildRouteLinks(Route(pointCount));

        links[0].FromIndex.ShouldBe(0);
        links[^1].ToIndex.ShouldBe(pointCount - 1);
        for (var k = 0; k < links.Count; k++)
        {
            links[k].Part.ShouldBe(k + 1);
            links[k].TotalParts.ShouldBe(links.Count);
            WaypointCount(links[k].Url).ShouldBeLessThanOrEqualTo(maxWaypoints);
            WaypointCount(links[k].Url).ShouldBe(links[k].ToIndex - links[k].FromIndex - 1);
            if (k > 0)
            {
                links[k].FromIndex.ShouldBe(links[k - 1].ToIndex);
            }
        }

        links.Count.ShouldBe((int)Math.Ceiling((pointCount - 1) / (double)(maxWaypoints + 1)));
    }

    [Fact]
    public void BuildRouteLinks_LongRoute_PartsAreEvenlySized()
    {
        var links = Builder(maxWaypoints: 9).BuildRouteLinks(Route(22)); // 21 legs → 3 parts of 7

        links.Select(l => l.ToIndex - l.FromIndex).ShouldBe([7, 7, 7]);
    }

    [Fact]
    public void BuildRouteLinks_SinglePoint_Throws() =>
        Should.Throw<ArgumentException>(() => Builder().BuildRouteLinks(Route(1)));

    [Fact]
    public void BuildDestinationLink_OmitsOriginSoMapsUsesCurrentLocation()
    {
        var url = Builder().BuildDestinationLink(new GeoPoint(53.13493, 17.98942));

        url.ShouldBe("https://www.google.com/maps/dir/?api=1&destination=53.13493%2C17.98942&travelmode=driving&dir_action=navigate");
    }

    private static int WaypointCount(string url)
    {
        var parameter = url.Split('&').FirstOrDefault(p => p.StartsWith("waypoints=", StringComparison.Ordinal));
        return parameter is null ? 0 : parameter.Split("%7C").Length;
    }
}
