using Kamada.Shared.Contracts.Delivery;
using Kamada.Web.Shared.Api;

namespace Kamada.Web.Shared.UnitTests;

public sealed class QueryStringTests
{
    [Fact]
    public void Build_NoValues_ReturnsEmpty() =>
        QueryString.Build(("search", null), ("status", "")).ShouldBeEmpty();

    [Fact]
    public void Build_DateOnly_UsesIsoFormatRegardlessOfCulture()
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.GetCultureInfo("en-US");

            QueryString.Build(("from", new DateOnly(2026, 9, 28))).ShouldBe("?from=2026-09-28");
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void Build_MixedValues_SkipsNullsAndEscapes() =>
        QueryString.Build(
                ("search", " CB 1 "),
                ("status", DeliveryRunStatus.InProgress),
                ("courierId", (Guid?)null),
                ("includeInactive", true),
                ("page", 2))
            .ShouldBe("?search=CB%201&status=InProgress&includeInactive=true&page=2");
}
