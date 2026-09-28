using Kamada.Shared.Contracts.Delivery;

namespace Kamada.Web.Shared.UnitTests;

public sealed class LabelsTests
{
    [Theory]
    [InlineData(0, "0 min")]
    [InlineData(89, "1 min")]
    [InlineData(90, "2 min")]
    [InlineData(59 * 60, "59 min")]
    [InlineData(60 * 60, "1 h 00 min")]
    [InlineData(65 * 60 + 10, "1 h 05 min")]
    [InlineData(3 * 3600 + 42 * 60, "3 h 42 min")]
    public void Duration_Seconds_FormatsMinutesAndHours(double seconds, string expected) =>
        Labels.Duration(seconds).ShouldBe(expected);

    [Theory]
    [InlineData(0, "0 m")]
    [InlineData(926.4, "926 m")]
    [InlineData(999.4, "999 m")]
    [InlineData(1000, "1,0 km")]
    [InlineData(19_034, "19,0 km")]
    [InlineData(20_351, "20,4 km")]
    public void Distance_Meters_UsesMetersBelowOneKilometreAndPolishDecimalComma(double meters, string expected) =>
        Labels.Distance(meters).ShouldBe(expected);

    [Fact]
    public void Of_EveryRunStatus_HasPolishLabel() =>
        Enum.GetValues<DeliveryRunStatus>().ShouldAllBe(s => Labels.Of(s) != s.ToString());

    [Fact]
    public void Of_EveryStopStatus_HasPolishLabel() =>
        Enum.GetValues<DeliveryStopStatus>().ShouldAllBe(s => Labels.Of(s) != s.ToString());

    [Theory]
    [InlineData("BruteForce")]
    [InlineData("HeldKarp")]
    [InlineData("NearestNeighbor+2opt+OrOpt")]
    [InlineData("Manual")]
    public void Algorithm_KnownSolver_IsTranslated(string algorithm) =>
        Labels.Algorithm(algorithm).ShouldNotBe(algorithm);

    [Fact]
    public void Algorithm_Unknown_IsShownAsIs() =>
        Labels.Algorithm("Quantum").ShouldBe("Quantum");

    [Fact]
    public void Date_DateOnly_UsesPolishFormat() =>
        Labels.Date(new DateOnly(2026, 9, 3)).ShouldBe("03.09.2026");
}
