using Kamada.Web.Shared.Api;
using Kamada.Web.Shared.Connectivity;

namespace Kamada.Web.Shared.Assistant;

/// <summary>When the order assistant runs on the server and when on the phone (pure, unit tested).</summary>
public static class AssistantPolicy
{
    /// <summary>
    /// Server in <see cref="AssistantMode.Auto"/> only while the connection is good and the server's assistant is switched on;
    /// the phone otherwise. <c>null</c>: nothing can be used (an explicit choice that is not available, or Auto with neither).
    /// </summary>
    /// <param name="serverEnabled">What the server's status said last; <c>null</c> when it could not be asked.</param>
    /// <param name="deviceReady">A language model is downloaded and the data it needs is on the phone.</param>
    public static AssistantLocation? Choose(AssistantMode mode, ConnectionQuality quality, bool? serverEnabled, bool deviceReady) => mode switch
    {
        AssistantMode.Server => AssistantLocation.Server,
        AssistantMode.Device => deviceReady ? AssistantLocation.Device : null,
        _ when quality == ConnectionQuality.Good && serverEnabled == true => AssistantLocation.Server,
        _ when deviceReady => AssistantLocation.Device,

        // Nothing local to fall back to: let the server answer (or explain why it cannot).
        _ => quality == ConnectionQuality.Good ? AssistantLocation.Server : null,
    };

    /// <summary>
    /// A server failure that the phone can make up for: no answer (transport error or timeout), the AI switched off or
    /// unavailable (503), or a gateway error. Validation and permission problems would fail the same way on the phone.
    /// </summary>
    public static bool ShouldFallBack(ApiProblem problem) => problem.Status is 0 or 502 or 503 or 504;
}
