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

    [Theory]
    [InlineData("Admin", "Administrator")]
    [InlineData("Courier", "Kurier")]
    [InlineData("Accountant", "Accountant")]
    [InlineData("1", "1")]
    public void Role_RoleClaimValue_IsTranslatedWhenKnown(string role, string expected) =>
        Labels.Role(role).ShouldBe(expected);

    [Fact]
    public void Date_DateOnly_UsesPolishFormat() =>
        Labels.Date(new DateOnly(2026, 9, 3)).ShouldBe("03.09.2026");

    [Fact]
    public void Time_Timestamp_ShowsLocalHoursAndMinutes() =>
        Labels.Time(Local(9, 5)).ShouldBe("09:05");

    [Fact]
    public void Arrival_RunStarted_ShowsClockTime() =>
        Labels.Arrival(Local(14, 0), 45 * 60 + 20).ShouldBe("ok. 14:45");

    [Fact]
    public void Arrival_RunNotStarted_ShowsOffsetFromStart() =>
        Labels.Arrival(null, 75 * 60).ShouldBe("1 h 15 min od startu");

    [Fact]
    public void Arrival_NoEstimate_ReturnsNull() =>
        Labels.Arrival(Local(14, 0), null).ShouldBeNull();

    [Theory]
    [InlineData(0, "0 przystanków")]
    [InlineData(1, "1 przystanek")]
    [InlineData(2, "2 przystanki")]
    [InlineData(4, "4 przystanki")]
    [InlineData(5, "5 przystanków")]
    [InlineData(12, "12 przystanków")]
    [InlineData(14, "14 przystanków")]
    [InlineData(22, "22 przystanki")]
    [InlineData(25, "25 przystanków")]
    public void StopCount_Count_UsesPolishPluralForm(int count, string expected) =>
        Labels.StopCount(count).ShouldBe(expected);

    [Theory]
    [InlineData("+48 601 111 111", "tel:+48601111111")]
    [InlineData(" +48 (52) 345-67-89", "tel:+48523456789")]
    [InlineData("601-111-111", "tel:601111111")]
    public void PhoneUri_FormattedNumber_KeepsDigitsAndLeadingPlus(string phone, string expected) =>
        Labels.PhoneUri(phone).ShouldBe(expected);

    private static DateTimeOffset Local(int hour, int minute)
    {
        var local = new DateTime(2026, 9, 28, hour, minute, 0, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local));
    }
}
