using System.Text.Json;
using Kamada.Shared.Matching.Drafts;

namespace Kamada.Modules.Assistant.Infrastructure.Gemini;

/// <summary>The shared <see cref="ExtractionPrompt"/> as a Gemini request: the same instruction, examples and JSON schema as Ollama.</summary>
internal static class GeminiPrompt
{
    private static readonly JsonElement Schema = JsonDocument.Parse(ExtractionPrompt.JsonSchema).RootElement.Clone();

    public static GeminiRequest Extraction(string text, AssistantLanguage language)
    {
        var contents = new List<GeminiContent>();
        foreach (var (input, output) in ExtractionPrompt.Examples)
        {
            contents.Add(GeminiContent.User(new GeminiPart(input)));
            contents.Add(GeminiContent.Model(output));
        }

        contents.Add(GeminiContent.User(new GeminiPart(ExtractionPrompt.UserMessage(text, language))));
        return new GeminiRequest(
            contents,
            new GeminiGenerationConfig(Temperature: 0, ResponseMimeType: "application/json", ResponseJsonSchema: Schema),
            SystemInstruction: new GeminiContent([new GeminiPart(ExtractionPrompt.System)]));
    }
}
