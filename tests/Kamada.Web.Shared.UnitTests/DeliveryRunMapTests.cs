using Kamada.Shared.Contracts.Delivery;
using Kamada.Shared.Contracts.Ordering;
using Kamada.Web.Shared.Maps;

namespace Kamada.Web.Shared.UnitTests;

public sealed class DeliveryRunMapTests
{
    private static readonly RouteLocationDto Base = Location("Magazyn", 53.12, 18.00);

    [Fact]
    public void Markers_SameStartAndEnd_OneEndpointMarkerAndNumberedStopsInGivenOrder()
    {
        var first = Stop(1, 53.13, 18.01);
        var second = Stop(2, 53.14, 18.02, DeliveryStopStatus.Delivered);
        var run = Run(Base, Base, first, second);

        var markers = DeliveryRunMap.Markers(run, [second, first]);

        markers.Count.ShouldBe(3);
        markers[0].Kind.ShouldBe(MapMarkerKind.Start);
        markers[0].Label.ShouldBe("Start i koniec: Magazyn");
        markers[1].Number.ShouldBe("1");
        markers[1].Kind.ShouldBe(MapMarkerKind.Done);
        markers[2].Number.ShouldBe("2");
        markers[2].Kind.ShouldBe(MapMarkerKind.Stop);
    }

    [Fact]
    public void Markers_StopWithoutCoordinates_IsSkippedButKeepsNumbering()
    {
        var first = Stop(1, null, null);
        var second = Stop(2, 53.14, 18.02, DeliveryStopStatus.Failed);
        var run = Run(Base, Location("Baza", 53.2, 18.1), first, second);

        var markers = DeliveryRunMap.Markers(run, [first, second]);

        markers.Select(m => m.Kind).ShouldBe([MapMarkerKind.Start, MapMarkerKind.Failed, MapMarkerKind.End]);
        markers[1].Number.ShouldBe("2");
    }

    [Fact]
    public void Route_StartStopsEnd_ReturnsPointsInOrder()
    {
        var stop = Stop(1, 53.13, 18.01);
        var run = Run(Base, Base, stop);

        var route = DeliveryRunMap.Route(run, [stop]);

        route.ShouldBe([new MapPoint(53.12, 18.00), new MapPoint(53.13, 18.01), new MapPoint(53.12, 18.00)]);
    }

    [Theory]
    [InlineData(0, "start")]
    [InlineData(3, "przystanek 3")]
    [InlineData(5, "przystanek 5")]
    [InlineData(6, "koniec")]
    public void PointName_NavigationPoint_NamesStartStopsAndEnd(int point, string expected) =>
        DeliveryRunMap.PointName(point, stopCount: 5).ShouldBe(expected);

    private static RouteLocationDto Location(string name, double latitude, double longitude) =>
        new(Guid.NewGuid(), name, "Długa", "1", "Bydgoszcz", "85-001", "PL", latitude, longitude, true, DateTimeOffset.UnixEpoch);

    private static DeliveryStopDto Stop(int sequence, double? latitude, double? longitude, DeliveryStopStatus status = DeliveryStopStatus.Pending) =>
        new(Guid.NewGuid(), Guid.NewGuid(), 100 + sequence, sequence, status, OrderStatus.OutForDelivery, $"Odbiorca {sequence}", "+48 600 000 000",
            "Krótka", sequence.ToString(System.Globalization.CultureInfo.InvariantCulture), null, "Bydgoszcz", "85-002",
            latitude, longitude, null, null, null, null, [], null);

    private static DeliveryRunDto Run(RouteLocationDto start, RouteLocationDto end, params DeliveryStopDto[] stops) =>
        new(Guid.NewGuid(), "Trasa", new DateOnly(2026, 9, 28), DeliveryRunStatus.InProgress, new CourierSummaryDto(Guid.NewGuid(), "Jan Kowalski", "+48 601 111 111"),
            "CB 12345", start, end, stops, null, [], null, null, DateTimeOffset.UnixEpoch);
}
