using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Domain;
using Kamada.Shared.Kernel.Results;

namespace Kamada.IntegrationTests.Infrastructure;

/// <summary>Instant "recognition" (no Whisper model download in tests): always hears <see cref="Text"/>.</summary>
internal sealed class FakeSpeechToText : ISpeechToText
{
    public const string Text = "Zamówienie dla Jana Kowalskiego: dwie kawy ziarniste";

    public bool IsReady => true;

    public Task PrepareAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<Result<Transcription>> TranscribeAsync(ReadOnlyMemory<byte> wav, AssistantLanguage language, CancellationToken cancellationToken) =>
        Task.FromResult<Result<Transcription>>(new Transcription(Text, language == AssistantLanguage.Auto ? "pl" : language.ToCode()));
}
