namespace Kamada.Modules.Assistant.Application.Abstractions;

/// <param name="Reachable">The model server answered.</param>
/// <param name="ModelPresent">The configured model is downloaded on that server.</param>
/// <param name="Provider">Who serves the model, e.g. <c>Ollama</c> or <c>Gemini</c>.</param>
internal sealed record LanguageModelProbeResult(bool Reachable, bool ModelPresent, string Model, string Provider);

/// <summary>Cheap availability check of the language model (used by <c>/health</c>).</summary>
internal interface ILanguageModelProbe
{
    Task<LanguageModelProbeResult> ProbeAsync(CancellationToken cancellationToken);
}
