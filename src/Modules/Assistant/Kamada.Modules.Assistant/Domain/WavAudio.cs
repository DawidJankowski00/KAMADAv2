using System.Buffers.Binary;

namespace Kamada.Modules.Assistant.Domain;

/// <summary>
/// Header facts of a RIFF/WAVE file. The browser records and encodes 16 kHz mono 16-bit PCM itself, so the server
/// only has to check that it got exactly that (no ffmpeg), how long it is and whether anything is audible.
/// </summary>
/// <param name="Peak">Loudest sample of 16-bit PCM, 0–1 (0 for other bit depths).</param>
internal sealed record WavAudio(int SampleRate, int Channels, int BitsPerSample, int DataLength, double Peak)
{
    public const int SpeechSampleRate = 16_000;

    /// <summary>
    /// Below about -46 dBFS nothing was captured (muted or wrong microphone). Even quiet speech peaks far higher;
    /// Whisper would spend seconds on such a recording and return nothing or a made-up phrase.
    /// </summary>
    public const double SilencePeak = 0.005;

    public bool IsSilent => Peak < SilencePeak;

    /// <summary>Peak in dBFS for logs (-inf for digital silence).</summary>
    public double PeakDecibels => 20 * Math.Log10(Peak);

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

                var length = Math.Min(size, file.Length - body);
                var peak = f.Bits == 16 ? PeakOf16Bit(file.Slice(body, length)) : 0;
                return new WavAudio(f.SampleRate, f.Channels, f.Bits, length, peak);
            }

            offset = body + size + (size % 2); // chunks are word-aligned
        }

        return null;
    }

    private static double PeakOf16Bit(ReadOnlySpan<byte> data)
    {
        var peak = 0;
        for (var i = 0; i + 1 < data.Length; i += 2)
        {
            peak = Math.Max(peak, Math.Abs((int)BinaryPrimitives.ReadInt16LittleEndian(data.Slice(i, 2))));
        }

        return peak / 32768.0;
    }
}
