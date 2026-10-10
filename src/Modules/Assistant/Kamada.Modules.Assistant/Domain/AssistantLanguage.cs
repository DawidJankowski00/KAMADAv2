namespace Kamada.Modules.Assistant.Domain;

/// <summary>Language of the utterance; <see cref="Auto"/> lets the model (or speech recognizer) detect it.</summary>
internal enum AssistantLanguage
{
    Auto,
    Polish,
    English,
}

internal static class AssistantLanguages
{
    /// <summary>Accepted request values (<c>pl</c>, <c>en</c>, <c>auto</c>); <c>null</c>/empty means auto.</summary>
    public static readonly IReadOnlyList<string> Codes = ["auto", "pl", "en"];

    public static bool IsValid(string? code) =>
        string.IsNullOrWhiteSpace(code) || Codes.Contains(code.Trim().ToLowerInvariant());

    public static AssistantLanguage Parse(string? code) => code?.Trim().ToLowerInvariant() switch
    {
        "pl" => AssistantLanguage.Polish,
        "en" => AssistantLanguage.English,
        _ => AssistantLanguage.Auto,
    };

    public static string ToCode(this AssistantLanguage language) => language switch
    {
        AssistantLanguage.Polish => "pl",
        AssistantLanguage.English => "en",
        _ => "auto",
    };
}
