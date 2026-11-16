using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Domain;
using Kamada.Shared.Matching.Drafts;
using Kamada.Shared.Kernel.Results;

namespace Kamada.IntegrationTests.Infrastructure;

/// <summary>Instant "recognition" (no Whisper model download in tests): always hears <see cref="Text"/>.</summary>
internal sealed class FakeSpeechToText : ISpeechToText
{
    public const string Text = "Zamówienie dla Jana Kowalskiego: dwie kawy ziarniste";

    public ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken) => ValueTask.FromResult(true);

    public Task<Result<Transcription>> TranscribeAsync(ReadOnlyMemory<byte> wav, AssistantLanguage language, CancellationToken cancellationToken) =>
        Task.FromResult<Result<Transcription>>(new Transcription(Text, language == AssistantLanguage.Auto ? "pl" : language.ToCode()));
}
