using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Kamada.Modules.Assistant.Application;
using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Domain;
using Kamada.Shared.Matching.Drafts;
using Kamada.Shared.Kernel.Results;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Whisper.net;
using Whisper.net.Ggml;

namespace Kamada.Modules.Assistant.Infrastructure.Whisper;

/// <summary>
/// whisper.cpp through Whisper.net, fully local. The GGML model is downloaded once into
/// <c>Assistant:Whisper:ModelPath</c> (a Docker volume) and loaded once; recognitions are limited by a semaphore
/// because each one uses several CPU threads. Audio and transcripts are neither stored nor logged.
/// </summary>
internal sealed partial class WhisperSpeechToText(
    IOptions<AssistantOptions> options,
    IHostEnvironment environment,
    IHttpClientFactory httpClientFactory,
    ILogger<WhisperSpeechToText> logger) : ISpeechToText, IDisposable
{
    public const string DownloadHttpClient = "assistant-whisper-download";

    private const long ProgressStepBytes = 50 * 1024 * 1024;

    private readonly WhisperOptions _settings = options.Value.Whisper;
    private readonly SemaphoreSlim _gate = new(options.Value.Whisper.MaxConcurrency);
    private WhisperFactory? _factory;

    public bool IsReady => Volatile.Read(ref _factory) is not null;

    private int Threads => _settings.Threads > 0 ? _settings.Threads : Math.Clamp(Environment.ProcessorCount / 2, 1, 8);

    public async Task PrepareAsync(CancellationToken cancellationToken)
    {
        var path = Path.GetFullPath(_settings.ModelPath, environment.ContentRootPath);
        if (!File.Exists(path))
        {
            await DownloadAsync(path, cancellationToken);
        }

        var stopwatch = Stopwatch.StartNew();
        Volatile.Write(ref _factory, WhisperFactory.FromPath(path));
        LogModelLoaded(_settings.Model, stopwatch.ElapsedMilliseconds, Threads);
    }

    public async Task<Result<Transcription>> TranscribeAsync(ReadOnlyMemory<byte> wav, AssistantLanguage language, CancellationToken cancellationToken)
    {
        var factory = Volatile.Read(ref _factory);
        if (factory is null)
        {
            return AssistantErrors.SpeechToTextNotReady;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var stopwatch = Stopwatch.StartNew();
            await using var processor = factory.CreateBuilder()
                .WithThreads(Threads)
                .WithLanguage(language == AssistantLanguage.Auto ? "auto" : language.ToCode())
                .Build();

            using var stream = MemoryMarshal.TryGetArray(wav, out var segment)
                ? new MemoryStream(segment.Array!, segment.Offset, segment.Count, writable: false)
                : new MemoryStream(wav.ToArray(), writable: false);

            var parts = new List<string>();
            var dropped = 0;
            string? detected = null;
            await foreach (var result in processor.ProcessAsync(stream, cancellationToken))
            {
                detected ??= result.Language;
                var text = result.Text.Trim();
                if (text.Length > 0 && !NonSpeechMarker().IsMatch(text))
                {
                    parts.Add(text);
                }
                else
                {
                    dropped++;
                }
            }

            LogTranscribed(stopwatch.ElapsedMilliseconds, detected, parts.Count, dropped);
            return new Transcription(string.Join(' ', parts), detected ?? language.ToCode());
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _factory?.Dispose();
        _gate.Dispose();
    }

    /// <summary>Downloads into a temporary file first, so an interrupted download never leaves a broken model behind.</summary>
    private async Task DownloadAsync(string path, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var type = Enum.Parse<GgmlType>(_settings.Model, ignoreCase: true);
        var temporary = path + ".download";
        LogDownloading(_settings.Model, path);

        var downloader = new WhisperGgmlDownloader(httpClientFactory.CreateClient(DownloadHttpClient));
        await using (var model = await downloader.GetGgmlModelAsync(type, QuantizationType.NoQuantization, cancellationToken))
        await using (var file = File.Create(temporary))
        {
            var buffer = new byte[1024 * 1024];
            long total = 0;
            var nextReport = ProgressStepBytes;
            int read;
            while ((read = await model.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                total += read;
                if (total >= nextReport)
                {
                    LogDownloadProgress(total / (1024 * 1024));
                    nextReport += ProgressStepBytes;
                }
            }
        }

        File.Move(temporary, path, overwrite: true);
        LogDownloaded(path);
    }

    /// <summary>Whisper marks silence or noise as "[BLANK_AUDIO]", "(muzyka)" and the like.</summary>
    [GeneratedRegex(@"^[\[\(\*].*[\]\)\*]$")]
    private static partial Regex NonSpeechMarker();

    [LoggerMessage(Level = LogLevel.Information, Message = "Downloading Whisper model '{Model}' to {Path} (first start only)")]
    private partial void LogDownloading(string model, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Whisper model download: {Megabytes} MB")]
    private partial void LogDownloadProgress(long megabytes);

    [LoggerMessage(Level = LogLevel.Information, Message = "Whisper model saved to {Path}")]
    private partial void LogDownloaded(string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Whisper model '{Model}' loaded in {ElapsedMs} ms ({Threads} threads per recognition)")]
    private partial void LogModelLoaded(string model, long elapsedMs, int threads);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Speech recognized in {ElapsedMs} ms (language: {Language}, {Segments} segments, {Dropped} non-speech)")]
    private partial void LogTranscribed(long elapsedMs, string? language, int segments, int dropped);
}
