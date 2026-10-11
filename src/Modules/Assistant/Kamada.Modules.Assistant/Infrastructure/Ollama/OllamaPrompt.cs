using System.Text.Json;
using Kamada.Shared.Matching.Drafts;
using Microsoft.Extensions.AI;

namespace Kamada.Modules.Assistant.Infrastructure.Ollama;

/// <summary>The shared <see cref="ExtractionPrompt"/> as <see cref="IChatClient"/> messages and response format.</summary>
internal static class OllamaPrompt
{
    public static ChatResponseFormat ResponseFormat { get; } =
        ChatResponseFormat.ForJsonSchema(JsonDocument.Parse(ExtractionPrompt.JsonSchema).RootElement.Clone(), "order", "Order mentioned in the note");

    public static IReadOnlyList<ChatMessage> Messages(string text, AssistantLanguage language)
    {
        var messages = new List<ChatMessage> { new(ChatRole.System, ExtractionPrompt.System) };
        foreach (var (input, output) in ExtractionPrompt.Examples)
        {
            messages.Add(new ChatMessage(ChatRole.User, input));
            messages.Add(new ChatMessage(ChatRole.Assistant, output));
        }

        messages.Add(new ChatMessage(ChatRole.User, ExtractionPrompt.UserMessage(text, language)));
        return messages;
    }
}
