using System.Diagnostics;
using Kamada.Modules.Assistant.Application;
using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Shared.Kernel.Results;
using Kamada.Shared.Matching.Drafts;
using Microsoft.Extensions.Logging;

namespace Kamada.Modules.Assistant.Infrastructure.Gemini;

/// <summary>
/// Extraction with Google Gemini: the prompt, examples and JSON schema of the local model (structured output), so
/// <c>OrderDraftResolver</c> works the same with either provider. Sends the note to Google; nothing is logged.
/// </summary>
internal sealed partial class GeminiOrderExtractor(
    GeminiClient gemini,
    IAssistantSettings settings,
    ILogger<GeminiOrderExtractor> logger) : IOrderExtractor
{
    public async Task<Result<ExtractedOrder>> ExtractAsync(string text, AssistantLanguage language, CancellationToken cancellationToken)
    {
        var model = (await settings.GetAsync(cancellationToken)).GeminiModel;
        var stopwatch = Stopwatch.StartNew();
        var result = await gemini.GenerateAsync(model, GeminiPrompt.Extraction(text, language), cancellationToken);
        if (result.Outcome is GeminiOutcome.Unavailable or GeminiOutcome.Refused)
        {
            return AssistantErrors.ModelUnavailable;
        }

        var parsed = ExtractionAnswer.Parse(result.Text);
        LogExtraction(model, stopwatch.ElapsedMilliseconds, parsed is not null);
        return parsed is null ? AssistantErrors.ExtractionFailed : parsed;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Order extraction by Gemini {Model} took {ElapsedMs} ms (valid: {Valid})")]
    private partial void LogExtraction(string model, long elapsedMs, bool valid);
}
