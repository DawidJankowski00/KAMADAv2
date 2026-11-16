namespace Kamada.Web.Shared.Platform;

/// <summary>Progress of a recording, reported by an <see cref="IAudioRecorder"/>.</summary>
public interface IAudioRecorderListener
{
    /// <param name="level">Recent peak level, 0–1.</param>
    /// <param name="seconds">Length recorded so far.</param>
    Task OnLevel(double level, double seconds);

    /// <summary>The maximum duration was reached; the listener should stop the recording.</summary>
    Task OnLimitReached();
}

/// <summary>
/// Microphone capture for dictation: the browser's AudioWorklet in the web apps, the platform recorder in the native
/// app. Always produces 16 kHz mono 16-bit PCM (the WAV header is added by <c>WavEncoder</c>). One recording at a time.
/// </summary>
public interface IAudioRecorder : IAsyncDisposable
{
    /// <returns>
    /// <c>ok</c>, or a reason it cannot record: <c>permission</c>, <c>no-device</c>, <c>insecure</c>, <c>unsupported</c>,
    /// <c>error</c> (see <c>RecorderMessages</c>).
    /// </returns>
    Task<string> StartAsync(TimeSpan maxDuration, IAudioRecorderListener listener);

    /// <summary>Stops and returns the samples (16 kHz mono 16-bit little-endian PCM); <c>null</c> when nothing was recorded.</summary>
    Task<byte[]?> StopAsync();

    /// <summary>Stops and drops the recording.</summary>
    Task DiscardAsync();
}
