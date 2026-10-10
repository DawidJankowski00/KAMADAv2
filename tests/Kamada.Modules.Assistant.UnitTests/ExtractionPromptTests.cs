using Kamada.Modules.Assistant.Domain;
using Kamada.Modules.Assistant.Infrastructure.Ollama;
using Microsoft.Extensions.AI;

namespace Kamada.Modules.Assistant.UnitTests;

public sealed class ExtractionPromptTests
{
    [Fact]
    public void Messages_StartWithSystemPromptAndEndWithTheNote()
    {
        var messages = ExtractionPrompt.Messages("dla Jana", AssistantLanguage.Polish);

        messages[0].Role.ShouldBe(ChatRole.System);
        messages[^1].Role.ShouldBe(ChatRole.User);
        messages[^1].Text.ShouldEndWith("dla Jana");
        messages[^1].Text.ShouldStartWith("(Polish note)");
    }

    [Fact]
    public void FewShotAnswers_AreValidExtractions()
    {
        var answers = ExtractionPrompt.Messages("x", AssistantLanguage.Auto).Where(m => m.Role == ChatRole.Assistant).ToList();

        answers.Count.ShouldBeGreaterThanOrEqualTo(2);
        foreach (var answer in answers)
        {
            var parsed = LlmOrderResponse.Parse(answer.Text);
            parsed.ShouldNotBeNull();
            parsed.ToDomain().Lines.ShouldNotBeEmpty();
        }
    }

    [Theory]
    [InlineData(null, nameof(AssistantLanguage.Auto))]
    [InlineData("PL", nameof(AssistantLanguage.Polish))]
    [InlineData(" en ", nameof(AssistantLanguage.English))]
    [InlineData("auto", nameof(AssistantLanguage.Auto))]
    public void Languages_Parse(string? code, string expected) =>
        AssistantLanguages.Parse(code).ShouldBe(Enum.Parse<AssistantLanguage>(expected));

    [Theory]
    [InlineData("de", false)]
    [InlineData("pl", true)]
    [InlineData(null, true)]
    public void Languages_IsValid(string? code, bool expected) =>
        AssistantLanguages.IsValid(code).ShouldBe(expected);
}
