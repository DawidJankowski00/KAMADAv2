using Kamada.Modules.Assistant.Application;
using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Domain.Settings;
using Kamada.Modules.Assistant.Infrastructure.Providers;
using Kamada.Shared.Kernel.Results;
using Kamada.Shared.Matching.Drafts;
using Microsoft.Extensions.DependencyInjection;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Kamada.Modules.Assistant.UnitTests;

public sealed class SelectedSpeechToTextTests
{
    private readonly CountingSpeech _whisper = new("whisper");
    private readonly CountingSpeech _gemini = new("gemini");

    [Fact]
    public async Task Transcribe_GeminiSelected_UsesGeminiAndNeverAsksWhisper()
    {
        var speech = Speech(SpeechProvider.Gemini);

        var ready = await speech.IsReadyAsync(TestContext.Current.CancellationToken);
        var result = await speech.TranscribeAsync(new byte[4], AssistantLanguage.Auto, TestContext.Current.CancellationToken);

        ready.ShouldBeTrue();
        result.Value.Text.ShouldBe("gemini");
        _whisper.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Transcribe_WhisperSelected_UsesWhisper()
    {
        var result = await Speech(SpeechProvider.Whisper).TranscribeAsync(new byte[4], AssistantLanguage.Auto, TestContext.Current.CancellationToken);

        result.Value.Text.ShouldBe("whisper");
        _gemini.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData(nameof(SpeechProvider.Off), true, "key")]
    [InlineData(nameof(SpeechProvider.Whisper), false, "key")]
    [InlineData(nameof(SpeechProvider.Gemini), true, null)]
    public async Task Transcribe_ProviderOffOrNotAllowedHere_FailsAsDisabledWithoutAskingAnyProvider(string provider, bool whisperEnabled, string? geminiKey)
    {
        var speech = Speech(Enum.Parse<SpeechProvider>(provider), whisperEnabled, geminiKey);

        var ready = await speech.IsReadyAsync(TestContext.Current.CancellationToken);
        var result = await speech.TranscribeAsync(new byte[4], AssistantLanguage.Auto, TestContext.Current.CancellationToken);

        ready.ShouldBeFalse();
        result.Error.ShouldBe(AssistantErrors.SpeechToTextDisabled);
        (_whisper.Calls + _gemini.Calls).ShouldBe(0);
    }

    private SelectedSpeechToText Speech(SpeechProvider provider, bool whisperEnabled = true, string? geminiKey = "key")
    {
        var services = new ServiceCollection()
            .AddKeyedSingleton<ISpeechToText>(SpeechProvider.Whisper, _whisper)
            .AddKeyedSingleton<ISpeechToText>(SpeechProvider.Gemini, _gemini)
            .BuildServiceProvider();
        var options = MsOptions.Create(new AssistantOptions
        {
            Enabled = true,
            Whisper = new WhisperOptions { Enabled = whisperEnabled },
            Gemini = new GeminiOptions { ApiKey = geminiKey },
        });
        return new SelectedSpeechToText(new StaticAssistantSettings(speechProvider: provider), options, services);
    }

    private sealed class CountingSpeech(string text) : ISpeechToText
    {
        public int Calls { get; private set; }

        public ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return ValueTask.FromResult(true);
        }

        public Task<Result<Transcription>> TranscribeAsync(ReadOnlyMemory<byte> wav, AssistantLanguage language, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<Result<Transcription>>(new Transcription(text, "pl"));
        }
    }
}
