using Kamada.Modules.Assistant.Application.Abstractions;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kamada.Modules.Assistant.Infrastructure;

/// <summary>
/// Language model server reachable and the configured model downloaded. Registered only when the assistant is
/// enabled, with failure status <c>Degraded</c>: the shop works without the assistant.
/// </summary>
internal sealed class AssistantHealthCheck(ILanguageModelProbe probe) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var result = await probe.ProbeAsync(cancellationToken);
        if (!result.Reachable)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "Language model server is not reachable.");
        }

        return result.ModelPresent
            ? HealthCheckResult.Healthy(result.Model)
            : new HealthCheckResult(context.Registration.FailureStatus, $"Model '{result.Model}' is not downloaded (ollama pull {result.Model}).");
    }
}
