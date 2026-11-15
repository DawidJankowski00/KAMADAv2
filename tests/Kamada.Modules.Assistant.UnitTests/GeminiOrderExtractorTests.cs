using System.Net;
using Kamada.Modules.Assistant.Application;
using Kamada.Modules.Assistant.Infrastructure.Gemini;
using Kamada.Shared.Matching.Drafts;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kamada.Modules.Assistant.UnitTests;

public sealed class GeminiOrderExtractorTests
{
    private const string Answer = """{"client":{"name":"Jan","surname":"Kowalski","phone":null,"email":null},"address":null,"items":[{"product":"kawa","quantity":2}],"notes":null}""";

    [Fact]
    public async Task Extract_ValidAnswer_ReturnsMentionsFromTheSelectedModel()
    {
        var handler = StubGeminiHandler.Answering(Answer);

        var result = await Extractor(handler).ExtractAsync("Jan Kowalski, dwie kawy", AssistantLanguage.Polish, TestContext.Current.CancellationToken);

        result.Value.Client.Surname.ShouldBe("Kowalski");
        result.Value.Lines.Single().ShouldBe(new ExtractedLine("kawa", 2));
        handler.Request!.Method.ShouldBe(HttpMethod.Post);
        handler.Request.RequestUri!.AbsolutePath.ShouldBe("/v1beta/models/gemini-test:generateContent");
    }

    [Fact]
    public async Task Extract_SendsKeyInHeaderAndTheSharedPromptWithJsonSchema()
    {
        var handler = StubGeminiHandler.Answering(Answer);

        await Extractor(handler).ExtractAsync("dwie kawy", AssistantLanguage.Auto, TestContext.Current.CancellationToken);

        handler.Request!.Headers.GetValues("x-goog-api-key").ShouldBe([StubGeminiHandler.ApiKey]);
        handler.Request.RequestUri!.Query.ShouldBeEmpty();
        var body = handler.RequestBody!.RootElement;
        body.GetProperty("systemInstruction").GetProperty("parts")[0].GetProperty("text").GetString().ShouldBe(ExtractionPrompt.System);
        var contents = body.GetProperty("contents");
        contents.GetArrayLength().ShouldBe(ExtractionPrompt.Examples.Count * 2 + 1);
        contents[1].GetProperty("role").GetString().ShouldBe("model");
        contents[contents.GetArrayLength() - 1].GetProperty("parts")[0].GetProperty("text").GetString().ShouldContain("dwie kawy");
        var config = body.GetProperty("generationConfig");
        config.GetProperty("temperature").GetDouble().ShouldBe(0);
        config.GetProperty("responseMimeType").GetString().ShouldBe("application/json");
        config.GetProperty("responseJsonSchema").GetProperty("required").GetArrayLength().ShouldBe(4);
    }

    [Fact]
    public async Task Extract_ThoughtPartsBeforeTheAnswer_AreIgnored()
    {
        var answer = System.Text.Json.JsonSerializer.Serialize(Answer);
        var handler = new StubGeminiHandler(HttpStatusCode.OK, $$$"""
            {"candidates":[{"content":{"role":"model","parts":[{"text":"Let me think…","thought":true},{"text":{{{answer}}}}]}}]}
            """);

        var result = await Extractor(handler).ExtractAsync("x", AssistantLanguage.Auto, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task Extract_AnswerIsNotAnOrder_FailsWithExtractionFailed()
    {
        var result = await Extractor(StubGeminiHandler.Answering("Nie wiem.")).ExtractAsync("x", AssistantLanguage.Auto, TestContext.Current.CancellationToken);

        result.Error.ShouldBe(AssistantErrors.ExtractionFailed);
    }

    [Fact]
    public async Task Extract_NoCandidates_FailsWithExtractionFailed()
    {
        var handler = new StubGeminiHandler(HttpStatusCode.OK, """{"promptFeedback":{"blockReason":"OTHER"}}""");

        var result = await Extractor(handler).ExtractAsync("x", AssistantLanguage.Auto, TestContext.Current.CancellationToken);

        result.Error.ShouldBe(AssistantErrors.ExtractionFailed);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task Extract_ApiRefusesOrFails_FailsWithModelUnavailable(HttpStatusCode status)
    {
        var handler = new StubGeminiHandler(status, """{"error":{"code":403,"status":"PERMISSION_DENIED","message":"..."}}""");

        var result = await Extractor(handler).ExtractAsync("x", AssistantLanguage.Auto, TestContext.Current.CancellationToken);

        result.Error.ShouldBe(AssistantErrors.ModelUnavailable);
    }

    [Fact]
    public async Task Extract_NoApiKey_FailsWithoutCallingTheApi()
    {
        var handler = StubGeminiHandler.Answering(Answer);
        var extractor = new GeminiOrderExtractor(
            StubGeminiHandler.Client(handler, apiKey: null), Settings(), NullLogger<GeminiOrderExtractor>.Instance);

        var result = await extractor.ExtractAsync("x", AssistantLanguage.Auto, TestContext.Current.CancellationToken);

        result.Error.ShouldBe(AssistantErrors.ModelUnavailable);
        handler.Request.ShouldBeNull();
    }

    private static StaticAssistantSettings Settings() => new(geminiModel: "gemini-test");

    private static GeminiOrderExtractor Extractor(StubGeminiHandler handler) =>
        new(StubGeminiHandler.Client(handler), Settings(), NullLogger<GeminiOrderExtractor>.Instance);
}
