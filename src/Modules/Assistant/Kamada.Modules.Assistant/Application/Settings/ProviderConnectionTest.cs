using System.Buffers.Binary;
using System.Diagnostics;
using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Domain.Settings;
using Kamada.Shared.Contracts.Assistant;
using Kamada.Shared.Kernel.Results;
using Kamada.Shared.Matching.Drafts;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.Assistant.Application.Settings;

/// <summary>
/// Checks the saved providers with real calls: a fixed sample note (no client data) through the extractor, and for
/// dictation the readiness of Whisper or one second of silence through Gemini. Works while the assistant is off, so
/// a developer can check a provider before switching it on.
/// </summary>
internal sealed class ProviderConnectionTest(
    IAssistantSettings settings,
    IOptions<AssistantOptions> options,
    IOrderExtractor extractor,
    ISpeechToText speechToText)
{
    internal const string SampleNote = "Dwie kawy ziarniste dla Jana Testowego, ulica Długa 1 w Gdańsku.";

    public async Task<AssistantProviderTestDto> RunAsync(CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync(cancellationToken);
        var text = await MeasureAsync(
            current.TextProvider.ToString(),
            current.TextModel,
            async () => (Result)await extractor.ExtractAsync(SampleNote, AssistantLanguage.Polish, cancellationToken));

        var speech = current.SpeechProvider switch
        {
            SpeechProvider.Off => null,
            SpeechProvider.Whisper => await MeasureAsync("Whisper", null, () => WhisperReadyAsync(current, cancellationToken)),
            _ => await MeasureAsync("Gemini", current.GeminiModel, async () =>
                (Result)await speechToText.TranscribeAsync(SilentWav(), AssistantLanguage.Polish, cancellationToken)),
        };

        return new AssistantProviderTestDto(text, speech);
    }

    private async Task<Result> WhisperReadyAsync(AssistantSettingsSnapshot current, CancellationToken cancellationToken)
    {
        if (!AssistantProviders.IsSpeechAllowed(options.Value, current))
        {
            return AssistantErrors.SpeechToTextDisabled;
        }

        return await speechToText.IsReadyAsync(cancellationToken) ? Result.Success() : AssistantErrors.SpeechToTextNotReady;
    }

    private static async Task<ProviderTestResultDto> MeasureAsync(string provider, string? model, Func<Task<Result>> call)
    {
        var stopwatch = Stopwatch.StartNew();
        var result = await call();
        return new ProviderTestResultDto(provider, model, result.IsSuccess, stopwatch.ElapsedMilliseconds, result.IsFailure ? result.Error.Code : null);
    }

    /// <summary>One second of 16 kHz mono 16-bit silence: the cheapest valid recording.</summary>
    internal static byte[] SilentWav()
    {
        const int sampleRate = 16_000;
        const int dataLength = sampleRate * 2;
        var wav = new byte[44 + dataLength];
        var span = wav.AsSpan();
        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], 36 + dataLength);
        "WAVEfmt "u8.CopyTo(span[8..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], 1);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], sampleRate * 2);
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], 2);
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], 16);
        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[40..], dataLength);
        return wav;
    }
}
