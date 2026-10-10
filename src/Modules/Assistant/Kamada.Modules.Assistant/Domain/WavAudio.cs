using System.Buffers.Binary;

namespace Kamada.Modules.Assistant.Domain;

/// <summary>
/// Header facts of a RIFF/WAVE file. The browser records and encodes 16 kHz mono 16-bit PCM itself, so the server
/// only has to check that it got exactly that (no ffmpeg) and how long it is.
/// </summary>
internal sealed record WavAudio(int SampleRate, int Channels, int BitsPerSample, int DataLength)
{
    public const int SpeechSampleRate = 16_000;

    public TimeSpan Duration => TimeSpan.FromSeconds((double)DataLength / (SampleRate * Channels * (BitsPerSample / 8)));

    /// <summary>What the speech model expects: 16 kHz, mono, 16-bit PCM.</summary>
    public bool IsSpeechFormat => SampleRate == SpeechSampleRate && Channels == 1 && BitsPerSample == 16;

    /// <summary>Reads the <c>fmt </c> and <c>data</c> chunks; <c>null</c> when this is not an uncompressed PCM WAV file.</summary>
    public static WavAudio? TryRead(ReadOnlySpan<byte> file)
    {
        if (file.Length < 12 || !file[..4].SequenceEqual("RIFF"u8) || !file[8..12].SequenceEqual("WAVE"u8))
        {
            return null;
        }

        (int SampleRate, int Channels, int Bits)? format = null;
        var offset = 12;
        while (offset + 8 <= file.Length)
        {
            var id = file.Slice(offset, 4);
            var size = BinaryPrimitives.ReadInt32LittleEndian(file.Slice(offset + 4, 4));
            var body = offset + 8;
            if (size < 0)
            {
                return null;
            }

            if (id.SequenceEqual("fmt "u8))
            {
                if (size < 16 || body + 16 > file.Length || BinaryPrimitives.ReadInt16LittleEndian(file.Slice(body, 2)) != 1)
                {
                    return null; // not PCM
                }

                format = (
                    BinaryPrimitives.ReadInt32LittleEndian(file.Slice(body + 4, 4)),
                    BinaryPrimitives.ReadInt16LittleEndian(file.Slice(body + 2, 2)),
                    BinaryPrimitives.ReadInt16LittleEndian(file.Slice(body + 14, 2)));
            }
            else if (id.SequenceEqual("data"u8))
            {
                if (format is not { } f || f.SampleRate <= 0 || f.Channels <= 0 || f.Bits is not (8 or 16 or 24 or 32))
                {
                    return null;
                }

                return new WavAudio(f.SampleRate, f.Channels, f.Bits, Math.Min(size, file.Length - body));
            }

            offset = body + size + (size % 2); // chunks are word-aligned
        }

        return null;
    }
}
