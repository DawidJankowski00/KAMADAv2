using Kamada.Modules.Assistant.Application;
using Kamada.Modules.Assistant.Domain;
using Kamada.Shared.Matching.Drafts;
using Kamada.Modules.Assistant.Infrastructure.Ollama;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using ChatRequest = OllamaSharp.Models.Chat.ChatRequest;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Kamada.Modules.Assistant.UnitTests;

public sealed class OllamaOrderExtractorTests
{
    private const string Answer = """{"client":{"name":"Jan","surname":"Kowalski","phone":null,"email":null},"address":null,"items":[{"product":"kawa","quantity":2}],"notes":null}""";

    [Fact]
    public async Task Extract_ValidAnswer_ReturnsMentionsAndAsksForDeterministicStructuredOutput()
    {
        var chat = new StubChatClient(_ => Answer);

        var result = await Extractor(chat).ExtractAsync("Jan Kowalski, dwie kawy", AssistantLanguage.Polish, TestContext.Current.CancellationToken);

        result.Value.Client.Surname.ShouldBe("Kowalski");
        result.Value.Lines.Single().ShouldBe(new ExtractedLine("kawa", 2));
        chat.Options!.Temperature.ShouldBe(0);
        chat.Options.ResponseFormat.ShouldBeOfType<ChatResponseFormatJson>().Schema.ShouldNotBeNull();
        chat.Options.RawRepresentationFactory.ShouldBeNull();
    }

    [Fact]
    public async Task Extract_ThinkConfigured_StartsFromNativeRequestWithThinkSwitch()
    {
        var chat = new StubChatClient(_ => Answer);

        await Extractor(chat, think: false).ExtractAsync("x", AssistantLanguage.Auto, TestContext.Current.CancellationToken);

        var native = chat.Options!.RawRepresentationFactory!.Invoke(chat).ShouldBeOfType<ChatRequest>();
        native.Think.ShouldNotBeNull();
        ((bool?)native.Think).ShouldBe(false);
    }

    [Fact]
    public async Task Extract_AnswerIsNotJson_FailsWithExtractionFailed()
    {
        var result = await Extractor(new StubChatClient(_ => "Przepraszam, nie rozumiem.")).ExtractAsync("x", AssistantLanguage.Auto, TestContext.Current.CancellationToken);

        result.Error.ShouldBe(AssistantErrors.ExtractionFailed);
    }

    [Fact]
    public async Task Extract_ServerUnreachable_FailsWithModelUnavailable()
    {
        var chat = new StubChatClient(_ => throw new HttpRequestException("connection refused"));

        var result = await Extractor(chat).ExtractAsync("x", AssistantLanguage.Auto, TestContext.Current.CancellationToken);

        result.Error.ShouldBe(AssistantErrors.ModelUnavailable);
    }

    [Fact]
    public async Task Extract_CallerCancels_PropagatesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var chat = new StubChatClient(_ => throw new TaskCanceledException());

        await Should.ThrowAsync<TaskCanceledException>(() => Extractor(chat).ExtractAsync("x", AssistantLanguage.Auto, cancellation.Token));
    }

    private static OllamaOrderExtractor Extractor(IChatClient chat, bool? think = null) => new(
        chat,
        MsOptions.Create(new AssistantOptions { Enabled = true, Ollama = new OllamaOptions { Think = think } }),
        NullLogger<OllamaOrderExtractor>.Instance);

    private sealed class StubChatClient(Func<IEnumerable<ChatMessage>, string> answer) : IChatClient
    {
        public ChatOptions? Options { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Options = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, answer(messages))));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
