using Kamada.Shared.Kernel.Domain;
using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Assistant.Domain.Settings;

/// <summary>
/// Runtime choices of the order assistant, changed by developers without a restart: whether it is on, which provider
/// reads notes and which one recognizes speech, and the model of each provider. Exactly one row (fixed id): the
/// configuration (<c>Assistant:*</c>) gives its starting values and stays the hard switch (<c>Assistant:Enabled</c>)
/// and the only place for secrets.
/// </summary>
internal sealed class AssistantSettings : Entity, IAuditable
{
    /// <summary>The id of the single row (a check constraint refuses any other).</summary>
    public static readonly Guid SingletonId = new("00000000-0000-7000-8000-000000000001");

    public const int MaxModelLength = 100;

    private AssistantSettings(bool enabled, TextProvider textProvider, string ollamaModel, string geminiModel, SpeechProvider speechProvider)
        : base(SingletonId)
    {
        Enabled = enabled;
        TextProvider = textProvider;
        OllamaModel = ollamaModel;
        GeminiModel = geminiModel;
        SpeechProvider = speechProvider;
    }

    public bool Enabled { get; private set; }

    public TextProvider TextProvider { get; private set; }

    /// <summary>Ollama model tag, e.g. <c>gemma3:4b</c>.</summary>
    public string OllamaModel { get; private set; }

    /// <summary>Gemini model code, e.g. <c>gemini-3.5-flash-lite</c>.</summary>
    public string GeminiModel { get; private set; }

    public SpeechProvider SpeechProvider { get; private set; }

    /// <summary>Worker who saved the settings last; <c>null</c> for the starting values.</summary>
    public Guid? UpdatedBy { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public static Result<AssistantSettings> Create(
        bool enabled, TextProvider textProvider, string ollamaModel, string geminiModel, SpeechProvider speechProvider)
    {
        var models = ValidateModels(ollamaModel, geminiModel);
        return models.IsFailure
            ? models.Error
            : new AssistantSettings(enabled, textProvider, ollamaModel.Trim(), geminiModel.Trim(), speechProvider);
    }

    public Result Update(
        bool enabled, TextProvider textProvider, string ollamaModel, string geminiModel, SpeechProvider speechProvider, Guid updatedBy)
    {
        var models = ValidateModels(ollamaModel, geminiModel);
        if (models.IsFailure)
        {
            return models;
        }

        Enabled = enabled;
        TextProvider = textProvider;
        OllamaModel = ollamaModel.Trim();
        GeminiModel = geminiModel.Trim();
        SpeechProvider = speechProvider;
        UpdatedBy = updatedBy;
        return Result.Success();
    }

    private static Result ValidateModels(string ollamaModel, string geminiModel) =>
        IsValidModel(ollamaModel) && IsValidModel(geminiModel)
            ? Result.Success()
            : AssistantSettingsErrors.InvalidModel;

    private static bool IsValidModel(string? model) =>
        !string.IsNullOrWhiteSpace(model) && model.Trim().Length <= MaxModelLength;
}
