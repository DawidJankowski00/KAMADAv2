namespace Kamada.Assistant.Evals;

/// <summary>
/// Command line: <c>--provider ollama|gemini --model gemma3:4b --ollama http://localhost:11434/ --connection "..."
/// --out results.md --geocode --no-think</c> (<c>--no-think</c> switches off reasoning of Ollama models such as qwen3).
/// Gemini reads its API key from the <c>GEMINI_API_KEY</c> environment variable only, so it never lands in shell history.
/// </summary>
internal sealed record EvalOptions(
    string Provider,
    string Model,
    Uri OllamaUrl,
    string? GeminiApiKey,
    string ConnectionString,
    string CasesPath,
    string? OutputPath,
    bool Geocode,
    bool NoThink)
{
    public const string Ollama = "ollama";
    public const string Gemini = "gemini";

    public const string DefaultConnection =
        "Host=localhost;Port=5432;Database=kamada_evals;Username=kamada;Password=kamada_dev;Gss Encryption Mode=Disable";

    public bool IsGemini => Provider == Gemini;

    /// <summary>Name in the report and the console.</summary>
    public string Label => (IsGemini ? $"Gemini {Model}" : Model) + (NoThink ? " (think off)" : string.Empty);

    public static EvalOptions Parse(string[] args)
    {
        string? Value(string name)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        var provider = (Value("--provider") ?? Ollama).ToLowerInvariant();
        if (provider is not (Ollama or Gemini))
        {
            throw new ArgumentException("--provider must be ollama or gemini.");
        }

        var geminiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
        if (provider == Gemini && string.IsNullOrWhiteSpace(geminiKey))
        {
            throw new ArgumentException("--provider gemini needs the GEMINI_API_KEY environment variable.");
        }

        return new EvalOptions(
            provider,
            Value("--model") ?? (provider == Gemini ? "gemini-3.5-flash-lite" : "gemma3:4b"),
            new Uri(Value("--ollama") ?? "http://localhost:11434/"),
            provider == Gemini ? geminiKey : null,
            Value("--connection") ?? DefaultConnection,
            Value("--cases") ?? Path.Combine(AppContext.BaseDirectory, "cases.json"),
            Value("--out"),
            args.Contains("--geocode"),
            args.Contains("--no-think"));
    }
}
