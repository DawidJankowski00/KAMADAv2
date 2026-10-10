using System.ComponentModel.DataAnnotations;

namespace Kamada.Modules.Assistant.Application;

internal sealed class AssistantOptions : IValidatableObject
{
    public const string SectionName = "Assistant";

    /// <summary>Switches the whole feature off: endpoints answer 503 and the admin panel hides the assistant.</summary>
    public bool Enabled { get; init; }

    [Range(100, 10_000)]
    public int MaxTextLength { get; init; } = 2000;

    public OllamaOptions Ollama { get; init; } = new();

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
}
