using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Assistant.Domain.Settings;

internal static class AssistantSettingsErrors
{
    public static readonly Error InvalidModel = Error.Validation(
        "AssistantSettings.InvalidModel", $"Model names are required and can have at most {AssistantSettings.MaxModelLength} characters.");
}
