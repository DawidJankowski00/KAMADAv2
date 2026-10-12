using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Domain;
using Kamada.Shared.Matching.Drafts;
using Kamada.Shared.Contracts.Assistant;
using Kamada.Shared.Kernel.Results;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.Assistant.Application.Transcriptions;

/// <summary>
/// Recording → text. Checks size, format, length and loudness before the expensive recognition; the audio stays in
/// memory only for the duration of the request. Only its length and peak level are logged (to tell a muted microphone
/// from unclear speech).
/// </summary>
internal sealed partial class TranscriptionHandler(
    ISpeechToText speechToText,
    IOptions<AssistantOptions> options,
    ILogger<TranscriptionHandler> logger)
{
    /// <param name="audio">Uploaded file content, <c>null</c> when the request has no <c>audio</c> field.</param>
    /// <param name="length">Declared size of the upload in bytes.</param>
    public async Task<Result<TranscriptionDto>> TranscribeAsync(Stream? audio, long length, string? language, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Whisper.Enabled)
        {
            return AssistantErrors.SpeechToTextDisabled;
        }

        if (audio is null || length == 0)
        {
            return AssistantErrors.AudioMissing;
        }

        if (length > settings.MaxAudioBytes)
        {
            return AssistantErrors.AudioTooLarge(settings.MaxAudioBytes);
        }

        if (!AssistantLanguages.IsValid(language))
        {
            return AssistantErrors.InvalidLanguage;
        }

        var wav = await ReadAsync(audio, settings.MaxAudioBytes, cancellationToken);
        if (wav is null)
        {
            return AssistantErrors.AudioTooLarge(settings.MaxAudioBytes);
        }

        var format = WavAudio.TryRead(wav);
        if (format is null || !format.IsSpeechFormat)
        {
            return AssistantErrors.InvalidAudio;
        }

        if (format.Duration > settings.MaxAudioDuration)
        {
            return AssistantErrors.AudioTooLong(settings.MaxAudioDuration);
        }

        if (!speechToText.IsReady)
        {
            return AssistantErrors.SpeechToTextNotReady;
        }

        LogRecording(format.Duration.TotalSeconds, format.PeakDecibels);
        if (format.IsSilent)
        {
            return AssistantErrors.SilentRecording;
        }

        var result = await speechToText.TranscribeAsync(wav, AssistantLanguages.Parse(language), cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        return string.IsNullOrWhiteSpace(result.Value.Text)
            ? AssistantErrors.NothingHeard
            : new TranscriptionDto(result.Value.Text, result.Value.Language, (long)format.Duration.TotalMilliseconds);
    }

    /// <summary>Reads at most <paramref name="maxBytes"/>; <c>null</c> when the stream is longer than declared.</summary>
    private static async Task<byte[]?> ReadAsync(Stream audio, int maxBytes, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        var chunk = new byte[81_920];
        int read;
        while ((read = await audio.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                return null;
            }

            buffer.Write(chunk, 0, read);
        }

        return buffer.ToArray();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Recording of {Seconds:0.0} s, peak {PeakDb:0.0} dBFS")]
    private partial void LogRecording(double seconds, double peakDb);
}
