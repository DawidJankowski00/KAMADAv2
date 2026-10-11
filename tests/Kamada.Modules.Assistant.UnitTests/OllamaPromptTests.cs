using Kamada.Modules.Assistant.Infrastructure.Ollama;
using Kamada.Shared.Matching.Drafts;
using Microsoft.Extensions.AI;

namespace Kamada.Modules.Assistant.UnitTests;

public sealed class OllamaPromptTests
{
    [Fact]
    public void Messages_StartWithSystemPromptAndEndWithTheNote()
    {
        var messages = OllamaPrompt.Messages("dla Jana", AssistantLanguage.Polish);

        messages[0].Role.ShouldBe(ChatRole.System);
        messages[^1].Role.ShouldBe(ChatRole.User);
        messages[^1].Text.ShouldBe("(Polish note)\ndla Jana");
        messages.Count(m => m.Role == ChatRole.Assistant).ShouldBe(ExtractionPrompt.Examples.Count);
    }
}
