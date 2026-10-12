using System.Diagnostics;
using Kamada.Modules.Assistant.Application;
using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Domain;
using Kamada.Shared.Matching.Drafts;
using Kamada.Shared.Kernel.Results;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp.Models.Chat;
using OllamaSharp.Models.Exceptions;

namespace Kamada.Modules.Assistant.Infrastructure.Ollama;

/// <summary>
/// Extraction with a local model served by Ollama, through <see cref="IChatClient"/>: structured output (JSON schema)
/// at temperature 0, so the same note gives the same answer. Neither the note nor the answer is logged (privacy).
/// </summary>
internal sealed partial class OllamaOrderExtractor(
    [FromKeyedServices(AssistantServiceKeys.ChatClient)] IChatClient chat,
    IOptions<AssistantOptions> settings,
    ILogger<OllamaOrderExtractor> logger) : IOrderExtractor
{
    public async Task<Result<ExtractedOrder>> ExtractAsync(string text, AssistantLanguage language, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        ChatResponse response;
        try
        {
            var options = new ChatOptions
            {
                Temperature = 0,
                ResponseFormat = OllamaPrompt.ResponseFormat,
                // Ollama-specific switch without an IChatClient equivalent: start from a native request.
                RawRepresentationFactory = settings.Value.Ollama.Think is { } think ? _ => new ChatRequest { Think = think } : null,
            };
            response = await chat.GetResponseAsync(OllamaPrompt.Messages(text, language), options, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or OllamaException
                                       || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            LogModelUnavailable(ex);
            return AssistantErrors.ModelUnavailable;
        }

        var parsed = ExtractionAnswer.Parse(response.Text);
        LogExtraction(stopwatch.ElapsedMilliseconds, parsed is not null, response.ModelId);
        return parsed is null ? AssistantErrors.ExtractionFailed : parsed;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Language model unavailable")]
    private partial void LogModelUnavailable(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Order extraction by {Model} took {ElapsedMs} ms (valid: {Valid})")]
    private partial void LogExtraction(long elapsedMs, bool valid, string? model);
}
