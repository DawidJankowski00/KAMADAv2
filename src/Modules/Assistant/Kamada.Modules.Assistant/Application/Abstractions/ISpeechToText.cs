using Kamada.Modules.Assistant.Domain;
using Kamada.Shared.Matching.Drafts;
using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Assistant.Application.Abstractions;

/// <param name="Language">Detected (or requested) language code, e.g. <c>pl</c>.</param>
internal sealed record Transcription(string Text, string Language);

/// <summary>Local speech recognition. Audio is processed in memory and never stored.</summary>
internal interface ISpeechToText
{
    /// <summary>False until the model is downloaded and loaded (the first start can take minutes).</summary>
    bool IsReady { get; }

    /// <summary>Downloads (if needed) and loads the model; called once in the background at startup.</summary>
    Task PrepareAsync(CancellationToken cancellationToken);

    /// <param name="wav">16 kHz mono 16-bit PCM WAV.</param>
    Task<Result<Transcription>> TranscribeAsync(ReadOnlyMemory<byte> wav, AssistantLanguage language, CancellationToken cancellationToken);
}
