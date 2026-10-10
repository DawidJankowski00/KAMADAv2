using System.Collections.Concurrent;
using Kamada.Modules.Assistant.Application;
using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Domain;
using Kamada.Shared.Kernel.Results;

namespace Kamada.IntegrationTests.Infrastructure;

/// <summary>
/// Stands in for the language model: each test registers the extraction for its own (unique) note text.
/// Unknown text behaves like an unusable model answer.
/// </summary>
internal sealed class FakeOrderExtractor : IOrderExtractor
{
    private readonly ConcurrentDictionary<string, ExtractedOrder> _extractions = new(StringComparer.Ordinal);

    public void Register(string text, ExtractedOrder extraction) => _extractions[text] = extraction;

    public Task<Result<ExtractedOrder>> ExtractAsync(string text, AssistantLanguage language, CancellationToken cancellationToken) =>
        Task.FromResult<Result<ExtractedOrder>>(
            _extractions.TryGetValue(text, out var extraction) ? extraction : AssistantErrors.ExtractionFailed);
}

/// <summary>The model is always "there" in tests, so <c>/health</c> stays Healthy.</summary>
internal sealed class FakeLanguageModelProbe : ILanguageModelProbe
{
    public Task<LanguageModelProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new LanguageModelProbeResult(true, true, "fake-model"));
}
