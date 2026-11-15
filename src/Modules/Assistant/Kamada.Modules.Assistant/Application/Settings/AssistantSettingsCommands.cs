using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Shared.Contracts.Assistant;
using Kamada.Shared.Infrastructure.Auth;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.Assistant.Application.Settings;

/// <summary>Developer settings: read and save. Saving takes effect on this API instance at once (cache invalidated).</summary>
internal sealed class AssistantSettingsCommands(
    IAssistantDbContext db,
    IAssistantSettings settings,
    IOptions<AssistantOptions> options,
    ICurrentUser currentUser)
{
    public async Task<AssistantSettingsDto> GetAsync(CancellationToken cancellationToken) =>
        (await settings.GetAsync(cancellationToken)).ToDto(options.Value);

    public async Task<Result<AssistantSettingsDto>> UpdateAsync(UpdateAssistantSettingsRequest request, CancellationToken cancellationToken)
    {
        // Creates the row from the configuration if nobody has read the settings yet.
        await settings.GetAsync(cancellationToken);
        var stored = await db.Settings.SingleAsync(cancellationToken);

        var updated = stored.Update(
            request.Enabled,
            request.TextProvider.ToDomain(),
            request.OllamaModel,
            request.GeminiModel,
            request.SpeechProvider.ToDomain(),
            currentUser.Id);
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        await db.SaveChangesAsync(cancellationToken);
        settings.Invalidate();
        return await GetAsync(cancellationToken);
    }
}
