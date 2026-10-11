namespace Kamada.Web.Shared.Assistant;

/// <summary>
/// The models the settings page offers. Files are put on the server by <c>deploy/assistant/fetch-models.sh</c>; a model
/// listed here must be handled there. Defaults and the "lite" choice come from the measurements in
/// <c>docs/on-device-assistant.md</c>.
/// </summary>
public static class LocalModels
{
    public static readonly IReadOnlyList<LocalModel> Language =
    [
        new("Qwen2.5-1.5B-Instruct-q4f16_1-MLC", LocalModelKind.Language, "Qwen 2.5 1,5B",
            "Dokładniejszy, wymaga ok. 2 GB pamięci karty graficznej.", 1670),
        new("Qwen2.5-0.5B-Instruct-q4f16_1-MLC", LocalModelKind.Language, "Qwen 2.5 0,5B (lekki)",
            "Najmniejszy: szybki i oszczędny, ale częściej się myli.", 540),
        new("Llama-3.2-1B-Instruct-q4f16_1-MLC", LocalModelKind.Language, "Llama 3.2 1B",
            "Średni rozmiar, słabszy w języku polskim.", 1340),
        new("gemma3-1b-it-q4f16_1-MLC", LocalModelKind.Language, "Gemma 3 1B",
            "Średni rozmiar, dobry w języku polskim.", 1150),
    ];

    public static readonly IReadOnlyList<LocalModel> Speech =
    [
        new("whisper-base", LocalModelKind.Speech, "Whisper base",
            "Dobry kompromis między szybkością a dokładnością.", 165, "onnx-community/whisper-base"),
        new("whisper-tiny", LocalModelKind.Speech, "Whisper tiny (lekki)",
            "Najszybszy, ale słabiej rozpoznaje nazwy własne i polskie znaki.", 105, "onnx-community/whisper-tiny"),
        new("whisper-small", LocalModelKind.Speech, "Whisper small",
            "Najdokładniejszy, wymaga mocnego telefonu.", 410, "onnx-community/whisper-small"),
    ];

    public static LocalModel DefaultLanguage => Language[0];

    public static LocalModel DefaultSpeech => Speech[0];

    public static LocalModel? Find(string? id) =>
        id is null ? null : Language.Concat(Speech).FirstOrDefault(m => m.Id == id);
}
