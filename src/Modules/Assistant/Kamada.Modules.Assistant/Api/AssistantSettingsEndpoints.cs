using Kamada.Modules.Assistant.Application.Settings;
using Kamada.Shared.Contracts.Assistant;
using Kamada.Shared.Infrastructure.Auth;
using Kamada.Shared.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Kamada.Modules.Assistant.Api;

internal static class AssistantSettingsEndpoints
{
    public static IEndpointRouteBuilder MapAssistantSettingsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/assistant/settings")
            .WithTags("Assistant")
            .RequireAuthorization(AuthorizationPolicies.DeveloperOnly);

        group.MapGet("/", async (AssistantSettingsCommands commands, CancellationToken ct) => TypedResults.Ok(await commands.GetAsync(ct)))
            .WithSummary("Developer settings of the order assistant (provider, models, switch)")
            .Produces<AssistantSettingsDto>();

        group.MapPut("/", async (UpdateAssistantSettingsRequest request, AssistantSettingsCommands commands, CancellationToken ct) =>
                (await commands.UpdateAsync(request, ct)).ToHttpResult(TypedResults.Ok))
            .WithValidation<UpdateAssistantSettingsRequest>()
            .WithSummary("Change the assistant's provider, models or switch – takes effect without a restart")
            .Produces<AssistantSettingsDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return endpoints;
    }
}
