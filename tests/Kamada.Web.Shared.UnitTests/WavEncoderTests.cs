using System.Buffers.Binary;
using Kamada.Web.Shared.Audio;

namespace Kamada.Web.Shared.UnitTests;

public sealed class WavEncoderTests
{
    [Fact]
    public void Encode_WritesA16kHzMono16BitPcmHeader()
    {
        var pcm = new byte[32_000]; // one second

        var wav = WavEncoder.Encode(pcm);

        wav.Length.ShouldBe(44 + pcm.Length);
        wav.AsSpan(0, 4).SequenceEqual("RIFF"u8).ShouldBeTrue();
        BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(4)).ShouldBe(36 + pcm.Length);
        wav.AsSpan(8, 8).SequenceEqual("WAVEfmt "u8).ShouldBeTrue();
        BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(20)).ShouldBe((short)1);
        BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(22)).ShouldBe((short)1);
        BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(24)).ShouldBe(16_000);
        BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(28)).ShouldBe(32_000);
        BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(32)).ShouldBe((short)2);
        BinaryPrimitives.ReadInt16LittleEndian(wav.AsSpan(34)).ShouldBe((short)16);
        wav.AsSpan(36, 4).SequenceEqual("data"u8).ShouldBeTrue();
        BinaryPrimitives.ReadInt32LittleEndian(wav.AsSpan(40)).ShouldBe(pcm.Length);
    }

    [Fact]
    public void Encode_KeepsTheSamples()
    {
        byte[] pcm = [1, 2, 3, 4];

        WavEncoder.Encode(pcm)[44..].ShouldBe(pcm);
    }

    [Fact]
    public void Encode_OddByteCount_Throws() =>
        Should.Throw<ArgumentException>(() => WavEncoder.Encode(new byte[3]));

    [Fact]
    public void Duration_IsComputedFromSampleCount() =>
        WavEncoder.Duration(48_000).ShouldBe(TimeSpan.FromSeconds(1.5));

    [Theory]
    [InlineData(7.4, "0:07 / 1:00")]
    [InlineData(60, "1:00 / 1:00")]
    public void Elapsed_FormatsMinutesAndSeconds(double seconds, string expected) =>
        RecorderMessages.Elapsed(TimeSpan.FromSeconds(seconds), TimeSpan.FromSeconds(60)).ShouldBe(expected);

    [Theory]
    [InlineData("permission")]
    [InlineData("insecure")]
    [InlineData("no-device")]
    [InlineData("unsupported")]
    public void RecorderMessages_KnownCodes_HaveSpecificPolishText(string code) =>
        RecorderMessages.For(code).ShouldNotBe(RecorderMessages.For("error"));
}
