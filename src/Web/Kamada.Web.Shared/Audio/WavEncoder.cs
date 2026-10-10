using System.Buffers.Binary;

namespace Kamada.Web.Shared.Audio;

/// <summary>Wraps raw PCM samples in a RIFF/WAVE container (the format the speech endpoint expects).</summary>
public static class WavEncoder
{
    public const int SpeechSampleRate = 16_000;

    private const int HeaderLength = 44;

    /// <param name="pcm">Little-endian 16-bit mono samples.</param>
    public static byte[] Encode(ReadOnlySpan<byte> pcm, int sampleRate = SpeechSampleRate)
    {
        if (pcm.Length % 2 != 0)
        {
            throw new ArgumentException("16-bit PCM must have an even number of bytes.", nameof(pcm));
        }

        const short channels = 1;
        const short bitsPerSample = 16;
        const short blockAlign = channels * bitsPerSample / 8;

        var wav = new byte[HeaderLength + pcm.Length];
        var span = wav.AsSpan();
        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], HeaderLength - 8 + pcm.Length);
        "WAVE"u8.CopyTo(span[8..]);
        "fmt "u8.CopyTo(span[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], 1); // PCM
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], channels);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], sampleRate * blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], blockAlign);
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], bitsPerSample);
        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[40..], pcm.Length);
        pcm.CopyTo(span[HeaderLength..]);
        return wav;
    }

    /// <summary>Length of 16-bit mono PCM at the given sample rate.</summary>
    public static TimeSpan Duration(int pcmBytes, int sampleRate = SpeechSampleRate) =>
        TimeSpan.FromSeconds(pcmBytes / 2.0 / sampleRate);
}
