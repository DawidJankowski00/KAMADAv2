using Kamada.Web.Shared.Api;
using Kamada.Web.Shared.Assistant;
using Kamada.Web.Shared.Connectivity;

namespace Kamada.Web.Shared.UnitTests.Assistant;

public sealed class AssistantPolicyTests
{
    // mode, quality, server enabled (null = unknown), device ready, expected (null = nothing usable)
    [Theory]
    [InlineData(nameof(AssistantMode.Auto), nameof(ConnectionQuality.Good), true, true, nameof(AssistantLocation.Server))]
    [InlineData(nameof(AssistantMode.Auto), nameof(ConnectionQuality.Good), true, false, nameof(AssistantLocation.Server))]
    [InlineData(nameof(AssistantMode.Auto), nameof(ConnectionQuality.Poor), true, true, nameof(AssistantLocation.Device))]
    [InlineData(nameof(AssistantMode.Auto), nameof(ConnectionQuality.Offline), true, true, nameof(AssistantLocation.Device))]
    [InlineData(nameof(AssistantMode.Auto), nameof(ConnectionQuality.Good), false, true, nameof(AssistantLocation.Device))]
    [InlineData(nameof(AssistantMode.Auto), nameof(ConnectionQuality.Good), null, true, nameof(AssistantLocation.Device))]
    [InlineData(nameof(AssistantMode.Auto), nameof(ConnectionQuality.Poor), true, false, null)]
    [InlineData(nameof(AssistantMode.Auto), nameof(ConnectionQuality.Offline), null, false, null)]
    [InlineData(nameof(AssistantMode.Auto), nameof(ConnectionQuality.Good), false, false, nameof(AssistantLocation.Server))]
    [InlineData(nameof(AssistantMode.Server), nameof(ConnectionQuality.Offline), true, true, nameof(AssistantLocation.Server))]
    [InlineData(nameof(AssistantMode.Device), nameof(ConnectionQuality.Good), true, true, nameof(AssistantLocation.Device))]
    [InlineData(nameof(AssistantMode.Device), nameof(ConnectionQuality.Good), true, false, null)]
    public void Choose_FollowsModeConnectionAndAvailability(string mode, string quality, bool? serverEnabled, bool deviceReady, string? expected)
    {
        var chosen = AssistantPolicy.Choose(Enum.Parse<AssistantMode>(mode), Enum.Parse<ConnectionQuality>(quality), serverEnabled, deviceReady);

        chosen.ShouldBe(expected is null ? null : Enum.Parse<AssistantLocation>(expected));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(502, true)]
    [InlineData(503, true)]
    [InlineData(504, true)]
    [InlineData(400, false)]
    [InlineData(401, false)]
    [InlineData(403, false)]
    [InlineData(409, false)]
    [InlineData(429, false)]
    [InlineData(500, false)]
    public void ShouldFallBack_OnlyWhenTheServerCannotAnswer(int status, bool expected) =>
        AssistantPolicy.ShouldFallBack(new ApiProblem(status, "x")).ShouldBe(expected);
}
