using Android.Media;
using Kamada.Web.Shared.Platform;

namespace Kamada.Mobile.Platform;

/// <summary>
/// Dictation with Android's <see cref="AudioRecord"/>: the microphone delivers 16 kHz mono 16-bit PCM directly (no
/// resampling, unlike the browser), tuned for speech recognition. Samples stay in memory until <see cref="StopAsync"/>.
/// </summary>
internal sealed class AndroidAudioRecorder : IAudioRecorder
{
    private const int SampleRate = 16_000;
    private static readonly TimeSpan LevelInterval = TimeSpan.FromMilliseconds(100);

    private readonly Lock _gate = new();
    private Recording? _current;

    public async Task<string> StartAsync(TimeSpan maxDuration, IAudioRecorderListener listener)
    {
        var permission = await MainThread.InvokeOnMainThreadAsync(Permissions.RequestAsync<Permissions.Microphone>);
        if (permission != PermissionStatus.Granted)
        {
            return "permission-app";
        }

        await DiscardAsync();

        var bufferSize = Math.Max(AudioRecord.GetMinBufferSize(SampleRate, ChannelIn.Mono, Encoding.Pcm16bit), SampleRate / 5);
        var record = new AudioRecord(AudioSource.VoiceRecognition, SampleRate, ChannelIn.Mono, Encoding.Pcm16bit, bufferSize * 2);
        if (record.State != State.Initialized)
        {
            record.Release();
            return "no-device";
        }

        try
        {
            record.StartRecording();
        }
        catch (Java.Lang.IllegalStateException)
        {
            record.Release();
            return "error";
        }

        var recording = new Recording(record, bufferSize, maxDuration, listener);
        lock (_gate)
        {
            _current = recording;
        }

        recording.Start();
        return "ok";
    }

    public async Task<byte[]?> StopAsync()
    {
        var recording = Take();
        return recording is null ? null : await recording.StopAsync();
    }

    public async Task DiscardAsync()
    {
        if (Take() is { } recording)
        {
            await recording.StopAsync();
        }
    }

    public ValueTask DisposeAsync() => new(DiscardAsync());

    private Recording? Take()
    {
        lock (_gate)
        {
            var recording = _current;
            _current = null;
            return recording;
        }
    }

    /// <summary>One recording: a background loop reads the microphone until stopped or the limit is reached.</summary>
    private sealed class Recording(AudioRecord record, int bufferSize, TimeSpan maxDuration, IAudioRecorderListener listener)
    {
        private readonly MemoryStream _pcm = new();
        private readonly CancellationTokenSource _stop = new();
        private Task? _loop;

        public void Start() => _loop = Task.Run(ReadLoopAsync);

        public async Task<byte[]> StopAsync()
        {
            await _stop.CancelAsync();
            if (_loop is not null)
            {
                await _loop;
            }

            record.Stop();
            record.Release();
            _stop.Dispose();
            return _pcm.ToArray();
        }

        private async Task ReadLoopAsync()
        {
            var buffer = new byte[bufferSize];
            var maxBytes = (long)(maxDuration.TotalSeconds * SampleRate * 2);
            var lastLevel = TimeSpan.Zero;
            var peak = 0;
            while (!_stop.IsCancellationRequested)
            {
                var read = await record.ReadAsync(buffer, 0, buffer.Length);
                if (read <= 0)
                {
                    break;
                }

                _pcm.Write(buffer, 0, read);
                for (var i = 0; i + 1 < read; i += 2)
                {
                    peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(buffer, i)));
                }

                var elapsed = TimeSpan.FromSeconds(_pcm.Length / (SampleRate * 2.0));
                if (elapsed - lastLevel >= LevelInterval)
                {
                    lastLevel = elapsed;
                    await listener.OnLevel(Math.Min(1, peak / 32768.0 * 1.5), elapsed.TotalSeconds);
                    peak = 0;
                }

                if (_pcm.Length >= maxBytes)
                {
                    // The listener stops the recording (StopAsync), which ends this loop.
                    _ = listener.OnLimitReached();
                    return;
                }
            }
        }
    }
}
