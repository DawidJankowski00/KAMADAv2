using FluentValidation;
using Kamada.Modules.Assistant.Domain.Settings;
using Kamada.Shared.Contracts.Assistant;

namespace Kamada.Modules.Assistant.Application.Settings;

internal sealed class UpdateAssistantSettingsRequestValidator : AbstractValidator<UpdateAssistantSettingsRequest>
{
    /// <summary>Ollama tags (<c>gemma3:4b</c>, <c>hf.co/user/model:Q4_K_M</c>) and Gemini codes (<c>gemini-3.5-flash-lite</c>).</summary>
    private const string ModelPattern = "^[A-Za-z0-9][A-Za-z0-9._:/-]*$";

    public UpdateAssistantSettingsRequestValidator()
    {
        RuleFor(r => r.TextProvider).IsInEnum();
        RuleFor(r => r.SpeechProvider).IsInEnum();
        RuleFor(r => r.OllamaModel).NotEmpty().MaximumLength(AssistantSettings.MaxModelLength).Matches(ModelPattern);
        RuleFor(r => r.GeminiModel).NotEmpty().MaximumLength(AssistantSettings.MaxModelLength).Matches(ModelPattern);
    }
}
