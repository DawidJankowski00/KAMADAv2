using System.ComponentModel.DataAnnotations;

namespace Kamada.Modules.Assistant.Application;

internal sealed class AssistantOptions : IValidatableObject
{
    public const string SectionName = "Assistant";

    /// <summary>Switches the whole feature off: endpoints answer 503 and the admin panel hides the assistant.</summary>
    public bool Enabled { get; init; }

    [Range(100, 10_000)]
    public int MaxTextLength { get; init; } = 2000;

    /// <summary>Upper limit of an uploaded recording (16 kHz mono 16-bit PCM is ~32 kB per second).</summary>
    [Range(1, 20 * 1024 * 1024)]
    public int MaxAudioBytes { get; init; } = 2 * 1024 * 1024;

    public TimeSpan MaxAudioDuration { get; init; } = TimeSpan.FromSeconds(60);

    public OllamaOptions Ollama { get; init; } = new();

    public WhisperOptions Whisper { get; init; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Ollama.Model))
        {
            yield return new ValidationResult("Assistant:Ollama:Model is required.", [nameof(Ollama)]);
        }

        if (Ollama.RequestTimeout <= TimeSpan.Zero)
        {
            yield return new ValidationResult("Assistant:Ollama:RequestTimeout must be positive.", [nameof(Ollama)]);
        }

        if (MaxAudioDuration <= TimeSpan.Zero)
        {
            yield return new ValidationResult("Assistant:MaxAudioDuration must be positive.", [nameof(MaxAudioDuration)]);
        }

        if (!WhisperOptions.Models.Contains(Whisper.Model, StringComparer.OrdinalIgnoreCase))
        {
            yield return new ValidationResult(
                $"Assistant:Whisper:Model must be one of: {string.Join(", ", WhisperOptions.Models)}.", [nameof(Whisper)]);
        }

        if (string.IsNullOrWhiteSpace(Whisper.ModelPath))
        {
            yield return new ValidationResult("Assistant:Whisper:ModelPath is required.", [nameof(Whisper)]);
        }

        // Data annotations are not validated recursively.
        if (Whisper.MaxConcurrency is < 1 or > 16 || Whisper.Threads is < 0 or > 64)
        {
            yield return new ValidationResult("Assistant:Whisper needs MaxConcurrency 1–16 and Threads 0–64.", [nameof(Whisper)]);
        }
    }
}

internal sealed class OllamaOptions
{
    /// <summary>Ollama server: the <c>ollama</c> container (compose profile <c>ai</c>) or a local install.</summary>
    public Uri BaseUrl { get; init; } = new("http://localhost:11434/");

    /// <summary>
    /// Any model from the Ollama library that supports structured output, e.g. <c>gemma3:4b</c> (default),
    /// <c>qwen3:4b</c> or a Bielik build. Switching is configuration only.
    /// </summary>
    public string Model { get; init; } = "gemma3:4b";

    /// <summary>CPU-only inference of a 4B model plus the first model load can take a while.</summary>
    public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Reasoning ("thinking") of models that support it, e.g. <c>qwen3</c>: <c>false</c> answers directly (several
    /// times faster on a CPU), <c>null</c> keeps the model's default. Do not set it for models without thinking.
    /// </summary>
    public bool? Think { get; init; }
}

internal sealed class WhisperOptions
{
    /// <summary>Multilingual GGML models of whisper.cpp that make sense on a CPU.</summary>
    public static readonly IReadOnlyList<string> Models = ["tiny", "base", "small", "medium"];

    /// <summary>Dictation can be switched off separately (e.g. on a machine with little RAM); typing still works.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary><c>small</c> (~470 MB) recognizes Polish well; <c>base</c> (~140 MB) is faster on weak CPUs.</summary>
    public string Model { get; init; } = "small";

    /// <summary>Model file; downloaded there on first start when missing. Relative paths start at the content root.</summary>
    public string ModelPath { get; init; } = "models/ggml-small.bin";

    /// <summary>Recognitions running at the same time; each one uses <see cref="Threads"/> CPU threads.</summary>
    public int MaxConcurrency { get; init; } = 1;

    /// <summary>0 = half of the CPU cores (at most 8).</summary>
    public int Threads { get; init; }
}
