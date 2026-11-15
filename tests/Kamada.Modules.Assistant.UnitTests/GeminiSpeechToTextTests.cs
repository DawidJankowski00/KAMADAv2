using System.Net;
using Kamada.Modules.Assistant.Application;
using Kamada.Modules.Assistant.Infrastructure.Gemini;
using Kamada.Shared.Matching.Drafts;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kamada.Modules.Assistant.UnitTests;

public sealed class GeminiSpeechToTextTests
{
    private static readonly byte[] Wav = [0x52, 0x49, 0x46, 0x46, 1, 2, 3, 4];

    [Fact]
    public async Task Transcribe_Answer_ReturnsTextAndLanguage()
    {
        var handler = StubGeminiHandler.Answering("""{"text":" Dwie kawy dla Jana Kowalskiego ","language":"pl"}""");

        var result = await Speech(handler).TranscribeAsync(Wav, AssistantLanguage.Auto, TestContext.Current.CancellationToken);

        result.Value.Text.ShouldBe("Dwie kawy dla Jana Kowalskiego");
        result.Value.Language.ShouldBe("pl");
        handler.Request!.RequestUri!.AbsolutePath.ShouldBe("/v1beta/models/gemini-test:generateContent");
    }

    [Fact]
    public async Task Transcribe_SendsTheRecordingInlineWithALanguageHint()
    {
        var handler = StubGeminiHandler.Answering("""{"text":"two coffees","language":"en"}""");

        await Speech(handler).TranscribeAsync(Wav, AssistantLanguage.English, TestContext.Current.CancellationToken);

        var parts = handler.RequestBody!.RootElement.GetProperty("contents")[0].GetProperty("parts");
        var audio = parts[0].GetProperty("inlineData");
        audio.GetProperty("mimeType").GetString().ShouldBe("audio/wav");
        Convert.FromBase64String(audio.GetProperty("data").GetString()!).ShouldBe(Wav);
        parts[1].GetProperty("text").GetString().ShouldContain("English");
        handler.RequestBody.RootElement.GetProperty("generationConfig").GetProperty("responseJsonSchema")
            .GetProperty("required").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Transcribe_UnusableAnswer_ReturnsEmptyTextInTheRequestedLanguage()
    {
        var result = await Speech(StubGeminiHandler.Answering("(cisza)")).TranscribeAsync(Wav, AssistantLanguage.English, TestContext.Current.CancellationToken);

        result.Value.Text.ShouldBeEmpty();
        result.Value.Language.ShouldBe("en");
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Transcribe_ApiRefusesOrFails_FailsWithSpeechToTextUnavailable(HttpStatusCode status)
    {
        var result = await Speech(new StubGeminiHandler(status, "{}")).TranscribeAsync(Wav, AssistantLanguage.Auto, TestContext.Current.CancellationToken);

        result.Error.ShouldBe(AssistantErrors.SpeechToTextUnavailable);
    }

    [Fact]
    public void IsReady_DependsOnTheApiKey()
    {
        var handler = StubGeminiHandler.Answering("{}");

        Speech(handler).IsReady.ShouldBeTrue();
        new GeminiSpeechToText(StubGeminiHandler.Client(handler, apiKey: null), Settings(), NullLogger<GeminiSpeechToText>.Instance)
            .IsReady.ShouldBeFalse();
    }

    private static StaticAssistantSettings Settings() => new(geminiModel: "gemini-test");

    private static GeminiSpeechToText Speech(StubGeminiHandler handler) =>
        new(StubGeminiHandler.Client(handler), Settings(), NullLogger<GeminiSpeechToText>.Instance);
}
