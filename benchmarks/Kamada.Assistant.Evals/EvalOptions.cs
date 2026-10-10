namespace Kamada.Assistant.Evals;

/// <summary>
/// Command line: <c>--model gemma3:4b --ollama http://localhost:11434/ --connection "..." --out results.md --geocode
/// --no-think</c> (<c>--no-think</c> switches off reasoning of models such as qwen3).
/// </summary>
internal sealed record EvalOptions(
    string Model,
    Uri OllamaUrl,
    string ConnectionString,
    string CasesPath,
    string? OutputPath,
    bool Geocode,
    bool NoThink)
{
    public const string DefaultConnection =
        "Host=localhost;Port=5432;Database=kamada_evals;Username=kamada;Password=kamada_dev;Gss Encryption Mode=Disable";

    public static EvalOptions Parse(string[] args)
    {
        string? Value(string name)
        {
            var index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }

        return new EvalOptions(
            Value("--model") ?? "gemma3:4b",
            new Uri(Value("--ollama") ?? "http://localhost:11434/"),
            Value("--connection") ?? DefaultConnection,
            Value("--cases") ?? Path.Combine(AppContext.BaseDirectory, "cases.json"),
            Value("--out"),
            args.Contains("--geocode"),
            args.Contains("--no-think"));
    }
}
