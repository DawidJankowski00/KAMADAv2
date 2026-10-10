using Kamada.Modules.Assistant.Domain;
using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Assistant.Application.Abstractions;

/// <summary>
/// Turns free text into mentions (client, address, products, notes). Implemented by a local language model;
/// it never sees the database and returns no ids.
/// </summary>
internal interface IOrderExtractor
{
    /// <returns>
    /// The extraction, <c>Assistant.ExtractionFailed</c> when the model's answer is unusable,
    /// or <c>Assistant.ModelUnavailable</c> when the model cannot be reached.
    /// </returns>
    Task<Result<ExtractedOrder>> ExtractAsync(string text, AssistantLanguage language, CancellationToken cancellationToken);
}
