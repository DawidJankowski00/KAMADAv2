namespace Kamada.Web.Shared.Audio;

/// <summary>A finished recording: 16 kHz mono 16-bit PCM WAV, ready to upload.</summary>
public sealed record RecordedAudio(byte[] Wav, TimeSpan Duration, string Language);
