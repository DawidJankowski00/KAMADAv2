using Kamada.Modules.Assistant.Domain;
using Kamada.Shared.Matching.Drafts;
using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Assistant.Application.Abstractions;

/// <param name="Language">Detected (or requested) language code, e.g. <c>pl</c>.</param>
internal sealed record Transcription(string Text, string Language);

/// <summary>Speech recognition (local Whisper or Gemini). Audio is processed in memory and never stored.</summary>
internal interface ISpeechToText
{
    /// <summary>
    /// False until the provider can recognize speech: the local model is downloaded and loaded (the first time can take
    /// minutes; asking starts it in the background), or the cloud provider has an API key.
    /// </summary>
    ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken);

    /// <param name="wav">16 kHz mono 16-bit PCM WAV.</param>
    Task<Result<Transcription>> TranscribeAsync(ReadOnlyMemory<byte> wav, AssistantLanguage language, CancellationToken cancellationToken);
}
