namespace Kamada.Modules.Assistant.Domain.Settings;

/// <summary>Who turns a note into an order draft.</summary>
internal enum TextProvider
{
    /// <summary>Local model served by Ollama (no data leaves the company's machines).</summary>
    Ollama,

    /// <summary>Google Gemini API (paid tier: prompts are not used for training).</summary>
    Gemini,
}
