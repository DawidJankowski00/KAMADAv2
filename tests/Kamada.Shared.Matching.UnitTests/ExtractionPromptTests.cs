using Kamada.Shared.Matching.Drafts;

namespace Kamada.Shared.Matching.UnitTests;

public sealed class ExtractionPromptTests
{
    [Theory]
    [InlineData(nameof(AssistantLanguage.Polish), "(Polish note)\ndla Jana")]
    [InlineData(nameof(AssistantLanguage.English), "(English note)\ndla Jana")]
    [InlineData(nameof(AssistantLanguage.Auto), "dla Jana")]
    public void UserMessage_StatesTheChosenLanguage(string language, string expected) =>
        ExtractionPrompt.UserMessage("dla Jana", Enum.Parse<AssistantLanguage>(language)).ShouldBe(expected);

    [Fact]
    public void FewShotAnswers_AreValidExtractions()
    {
        ExtractionPrompt.Examples.Count.ShouldBeGreaterThanOrEqualTo(2);
        foreach (var (_, answer) in ExtractionPrompt.Examples)
        {
            var parsed = ExtractionAnswer.Parse(answer);
            parsed.ShouldNotBeNull();
            parsed.Lines.ShouldNotBeEmpty();
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
