using System.Buffers.Binary;
using Kamada.Modules.Assistant.Application;
using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Application.Transcriptions;
using Kamada.Modules.Assistant.Domain;
using Kamada.Shared.Matching.Drafts;
using Kamada.Shared.Kernel.Results;
using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Kamada.Modules.Assistant.UnitTests;

public sealed class TranscriptionHandlerTests
{
    private readonly StubSpeechToText _speech = new();

    [Fact]
    public async Task Transcribe_ValidRecording_ReturnsTextLanguageAndDuration()
    {
        var result = await TranscribeAsync(Wav(seconds: 2.5), "pl");

        result.IsSuccess.ShouldBeTrue();
        result.Value.Text.ShouldBe("dwie kawy");
        result.Value.Language.ShouldBe("pl");
        result.Value.DurationMs.ShouldBe(2500);
        _speech.LastLanguage.ShouldBe(AssistantLanguage.Polish);
    }

    [Fact]
    public async Task Transcribe_NoFile_FailsWithAudioMissing() =>
        (await Handler(Options()).TranscribeAsync(null, 0, null, TestContext.Current.CancellationToken))
            .Error.Code.ShouldBe("Assistant.AudioMissing");

    [Fact]
    public async Task Transcribe_DeclaredSizeOverLimit_FailsBeforeReading() =>
        (await TranscribeAsync(Wav(seconds: 1), maxBytes: 1000)).Error.Code.ShouldBe("Assistant.AudioTooLarge");

    [Fact]
    public async Task Transcribe_LongerThanLimit_FailsWithAudioTooLong() =>
        (await TranscribeAsync(Wav(seconds: 61))).Error.Code.ShouldBe("Assistant.AudioTooLong");

    [Theory]
    [InlineData(44_100, 1)]
    [InlineData(16_000, 2)]
    public async Task Transcribe_NotSpeechFormat_FailsWithInvalidAudio(int sampleRate, int channels) =>
        (await TranscribeAsync(Wav(seconds: 1, sampleRate, channels))).Error.Code.ShouldBe("Assistant.InvalidAudio");

    [Fact]
    public async Task Transcribe_NotAWavFile_FailsWithInvalidAudio() =>
        (await TranscribeAsync("this is not audio"u8.ToArray())).Error.Code.ShouldBe("Assistant.InvalidAudio");

    [Fact]
    public async Task Transcribe_UnknownLanguage_Fails() =>
        (await TranscribeAsync(Wav(seconds: 1), "de")).Error.Code.ShouldBe("Assistant.InvalidLanguage");

    [Fact]
    public async Task Transcribe_ModelNotReady_FailsAsUnavailable()
    {
        _speech.IsReady = false;

        var result = await TranscribeAsync(Wav(seconds: 1));

        result.Error.ShouldBe(AssistantErrors.SpeechToTextNotReady);
        _speech.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Transcribe_NoSpeechRecognized_FailsWithNothingHeard()
    {
        _speech.Text = "  ";

        (await TranscribeAsync(Wav(seconds: 1))).Error.Code.ShouldBe("Assistant.NothingHeard");
    }

    [Fact]
    public async Task Transcribe_SilentRecording_FailsWithoutTouchingTheModel()
    {
        var result = await TranscribeAsync(Wav(seconds: 2, amplitude: 50));

        result.Error.ShouldBe(AssistantErrors.SilentRecording);
        _speech.Calls.ShouldBe(0);
    }

    [Fact]
    public async Task Transcribe_DictationDisabled_FailsWithoutTouchingTheModel()
    {
        var options = Options(whisperEnabled: false);

        var result = await Handler(options).TranscribeAsync(
            new MemoryStream(Wav(seconds: 1)), 100, null, TestContext.Current.CancellationToken);

        result.Error.ShouldBe(AssistantErrors.SpeechToTextDisabled);
        _speech.Calls.ShouldBe(0);
    }

    [Fact]
    public void WavAudio_ReadsHeaderAndDuration()
    {
        var wav = WavAudio.TryRead(Wav(seconds: 1.5));

        wav.ShouldNotBeNull();
        wav.IsSpeechFormat.ShouldBeTrue();
        wav.Duration.ShouldBe(TimeSpan.FromSeconds(1.5));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(100, true)]
    [InlineData(1000, false)]
    public void WavAudio_PeakLevel_TellsSilenceFromSound(int amplitude, bool silent)
    {
        var wav = WavAudio.TryRead(Wav(seconds: 1, amplitude: amplitude))!;

        wav.Peak.ShouldBe(amplitude / 32768.0, tolerance: 0.0001);
        wav.IsSilent.ShouldBe(silent);
    }

    [Fact]
    public void WavAudio_SkipsUnknownChunksBeforeData()
    {
        var plain = Wav(seconds: 1);
        byte[] list = [.. "LIST"u8.ToArray(), 4, 0, 0, 0, .. "INFO"u8.ToArray()];
        byte[] withList = [.. plain[..36], .. list, .. plain[36..]];

        WavAudio.TryRead(withList)!.Duration.ShouldBe(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void WavAudio_CompressedFormat_IsRejected()
    {
        var wav = Wav(seconds: 1);
        BinaryPrimitives.WriteInt16LittleEndian(wav.AsSpan(20), 3); // IEEE float

        WavAudio.TryRead(wav).ShouldBeNull();
    }

    /// <summary>PCM WAV with the given format holding a square wave of the given amplitude (0 = digital silence).</summary>
    internal static byte[] Wav(double seconds, int sampleRate = 16_000, int channels = 1, int amplitude = 8000)
    {
        var dataLength = (int)(seconds * sampleRate) * channels * 2;
        var wav = new byte[44 + dataLength];
        var span = wav.AsSpan();
        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], 36 + dataLength);
        "WAVEfmt "u8.CopyTo(span[8..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], 1);
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], (short)channels);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], sampleRate * channels * 2);
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], (short)(channels * 2));
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], 16);
        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[40..], dataLength);
        for (var i = 0; i < dataLength / 2; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(span[(44 + i * 2)..], (short)(i / 40 % 2 == 0 ? amplitude : -amplitude));
        }

        return wav;
    }

    private TranscriptionHandler Handler(Microsoft.Extensions.Options.IOptions<AssistantOptions> options) =>
        new(_speech, options, NullLogger<TranscriptionHandler>.Instance);

    private Task<Result<Kamada.Shared.Contracts.Assistant.TranscriptionDto>> TranscribeAsync(
        byte[] audio, string? language = null, int maxBytes = 2 * 1024 * 1024) =>
        Handler(Options(maxBytes: maxBytes))
            .TranscribeAsync(new MemoryStream(audio), audio.Length, language, TestContext.Current.CancellationToken);

    private static Microsoft.Extensions.Options.IOptions<AssistantOptions> Options(int maxBytes = 2 * 1024 * 1024, bool whisperEnabled = true) =>
        MsOptions.Create(new AssistantOptions
        {
            Enabled = true,
            MaxAudioBytes = maxBytes,
            MaxAudioDuration = TimeSpan.FromSeconds(60),
            Whisper = new WhisperOptions { Enabled = whisperEnabled },
        });

    private sealed class StubSpeechToText : ISpeechToText
    {
        public bool IsReady { get; set; } = true;

        public string Text { get; set; } = "dwie kawy";

        public int Calls { get; private set; }

        public AssistantLanguage? LastLanguage { get; private set; }

        public Task PrepareAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<Result<Transcription>> TranscribeAsync(ReadOnlyMemory<byte> wav, AssistantLanguage language, CancellationToken cancellationToken)
        {
            Calls++;
            LastLanguage = language;
            return Task.FromResult<Result<Transcription>>(new Transcription(Text, language == AssistantLanguage.Auto ? "pl" : language.ToCode()));
        }
    }
}
