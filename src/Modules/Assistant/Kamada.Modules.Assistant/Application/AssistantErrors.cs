using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Assistant.Application;

internal static class AssistantErrors
{
    public static readonly Error Disabled = Error.Unavailable(
        "Assistant.Disabled", "The order assistant is switched off (Assistant:Enabled).");

    public static readonly Error ModelUnavailable = Error.Unavailable(
        "Assistant.ModelUnavailable", "The local language model is not reachable or not loaded. Try again in a moment.");

    public static readonly Error ExtractionFailed = Error.Validation(
        "Assistant.ExtractionFailed", "The language model returned an answer that is not a valid order.");

    public static readonly Error NothingRecognized = Error.Validation(
        "Assistant.NothingRecognized", "No client, address or product was recognized in the text.");
}
