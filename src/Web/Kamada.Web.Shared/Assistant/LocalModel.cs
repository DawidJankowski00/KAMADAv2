namespace Kamada.Web.Shared.Assistant;

public enum LocalModelKind
{
    /// <summary>Extracts client, address and products from the note (WebLLM).</summary>
    Language,

    /// <summary>Dictation (Whisper through transformers.js).</summary>
    Speech,
}

/// <summary>A model the phone can download and run (see <see cref="LocalModels"/>).</summary>
/// <param name="Id">Folder under <c>/models/llm</c> (MLC model id) or the Whisper name.</param>
/// <param name="Repo">Speech models: the Hugging Face repository path used as the file prefix under <c>/models/stt</c>.</param>
/// <param name="ContextWindow">Language models: tokens the prompt and answer may use together.</param>
/// <param name="DownloadMb">Approximate size of the download.</param>
public sealed record LocalModel(string Id, LocalModelKind Kind, string Title, string Description, int DownloadMb, string? Repo = null, int ContextWindow = 4096);
