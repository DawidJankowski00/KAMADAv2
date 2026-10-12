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
        new("Qwen3-1.7B-q4f16_1-MLC", LocalModelKind.Language, "Qwen 3 1,7B",
            "Najdokładniejszy z dostępnych, wymaga ok. 2 GB pamięci karty graficznej.", 945),
        new("Qwen3-0.6B-q4f16_1-MLC", LocalModelKind.Language, "Qwen 3 0,6B (lekki)",
            "Mały i szybki dla słabszych telefonów, ale dokładność jest niższa.", 340),
        new("Qwen2.5-1.5B-Instruct-q4f16_1-MLC", LocalModelKind.Language, "Qwen 2.5 1,5B",
            "Starszy model o podobnym rozmiarze.", 850),
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
