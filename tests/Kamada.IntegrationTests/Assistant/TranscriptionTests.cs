using System.Buffers.Binary;
using System.Net;
using System.Net.Http.Headers;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Assistant;

namespace Kamada.IntegrationTests.Assistant;

/// <summary>Multipart upload and limits of the dictation endpoint; recognition itself is <see cref="FakeSpeechToText"/>.</summary>
public sealed class TranscriptionTests(KamadaApiFactory factory)
{
    private const string Url = "/api/assistant/transcriptions";

    [Fact]
    public async Task Transcribe_ValidRecording_ReturnsRecognizedText()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);

        var response = await admin.PostAsync(Url, Upload(Wav(seconds: 3), language: "pl"), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var transcription = await response.ReadAsAsync<TranscriptionDto>();
        transcription.ShouldBe(new TranscriptionDto(FakeSpeechToText.Text, "pl", 3000));
    }

    [Fact]
    public async Task Status_WhenSpeechModelIsReady_ReportsSpeechToText()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);

        var status = await (await admin.GetAsync("/api/assistant/status", TestContext.Current.CancellationToken)).ReadAsAsync<AssistantStatusDto>();

        status.SpeechToText.ShouldBeTrue();
    }

    [Fact]
    public async Task Transcribe_RecordingLongerThan60Seconds_Returns400()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);

        var response = await admin.PostAsync(Url, Upload(Wav(seconds: 61)), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemCodeAsync()).ShouldBe("Assistant.AudioTooLong");
    }

    [Fact]
    public async Task Transcribe_SilentRecording_Returns400()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);

        var response = await admin.PostAsync(Url, Upload(Wav(seconds: 2, amplitude: 0)), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemCodeAsync()).ShouldBe("Assistant.SilentRecording");
    }

    [Fact]
    public async Task Transcribe_FileOver2MB_Returns400()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);

        var response = await admin.PostAsync(Url, Upload(new byte[(2 * 1024 * 1024) + 1]), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemCodeAsync()).ShouldBe("Assistant.AudioTooLarge");
    }

    [Fact]
    public async Task Transcribe_WrongSampleRate_Returns400()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);

        var response = await admin.PostAsync(Url, Upload(Wav(seconds: 1, sampleRate: 44_100)), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemCodeAsync()).ShouldBe("Assistant.InvalidAudio");
    }

    [Fact]
    public async Task Transcribe_WithoutAudioField_Returns400()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        using var form = new MultipartFormDataContent { { new StringContent("pl"), "language" } };

        var response = await admin.PostAsync(Url, form, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemCodeAsync()).ShouldBe("Assistant.AudioMissing");
    }

    [Fact]
    public async Task Transcribe_AsCourier_Returns403()
    {
        var (courier, _) = await factory.CreateSignedInClientAsync(WorkerRole.Courier);

        var response = await courier.PostAsync(Url, Upload(Wav(seconds: 1)), TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static MultipartFormDataContent Upload(byte[] audio, string? language = null)
    {
        var file = new ByteArrayContent(audio);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        var form = new MultipartFormDataContent { { file, "audio", "nagranie.wav" } };
        if (language is not null)
        {
            form.Add(new StringContent(language), "language");
        }

        return form;
    }

    /// <summary>16-bit PCM mono WAV with a square wave (amplitude 0 = digital silence).</summary>
    private static byte[] Wav(double seconds, int sampleRate = 16_000, int amplitude = 8000)
    {
        var dataLength = (int)(seconds * sampleRate) * 2;
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
        for (var i = 0; i < dataLength / 2; i++)
        {
            BinaryPrimitives.WriteInt16LittleEndian(span[(44 + i * 2)..], (short)(i / 40 % 2 == 0 ? amplitude : -amplitude));
        }

        return wav;
    }
}
