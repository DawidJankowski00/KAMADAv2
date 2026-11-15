using Kamada.Modules.Assistant.Application;
using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Application.Drafts;
using Kamada.Modules.Assistant.Application.Transcriptions;
using Kamada.Shared.Contracts.Assistant;
using Kamada.Shared.Infrastructure.Auth;
using Kamada.Shared.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.Assistant.Api;

internal static class AssistantEndpoints
{
    /// <summary>Hard request limit (multipart overhead included); the configured audio limits are checked by the handler.</summary>
    private const long MaxUploadBytes = 25 * 1024 * 1024;

    public static IEndpointRouteBuilder MapOrderAssistantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/assistant")
            .WithTags("Assistant")
            .RequireAuthorization(AuthorizationPolicies.SalesAccess);

        group.MapGet("/status", async (AssistantStatusQuery query, CancellationToken ct) => TypedResults.Ok(await query.GetAsync(ct)))
            .WithSummary("Whether the order assistant (and dictation) is available and which model it uses")
            .Produces<AssistantStatusDto>();

        // Inference is expensive: these endpoints are rate-limited and answer 503 when the feature is off.
        var inference = group.MapGroup("/")
            .RequireRateLimiting(RateLimitPolicies.Assistant)
            .AddEndpointFilter(async (context, next) =>
            {
                var services = context.HttpContext.RequestServices;
                var settings = await services.GetRequiredService<IAssistantSettings>().GetAsync(context.HttpContext.RequestAborted);
                return services.GetRequiredService<IOptions<AssistantOptions>>().Value.Enabled && settings.Enabled
                    ? await next(context)
                    : AssistantErrors.Disabled.ToProblem();
            })
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        inference.MapPost("/order-drafts", async (OrderDraftRequest request, OrderDraftHandler handler, CancellationToken ct) =>
                (await handler.CreateAsync(request, ct)).ToHttpResult(TypedResults.Ok))
            .WithValidation<OrderDraftRequest>()
            .WithSummary("Turn a dictated or typed note into an order draft (client, address, products) – nothing is saved")
            .Produces<OrderDraftDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        // Multipart upload authenticated by a bearer token, so antiforgery does not apply.
        inference.MapPost("/transcriptions", async (IFormFile? audio, [FromForm] string? language, TranscriptionHandler handler, CancellationToken ct) =>
            {
                await using var content = audio?.OpenReadStream();
                return (await handler.TranscribeAsync(content, audio?.Length ?? 0, language, ct)).ToHttpResult(TypedResults.Ok);
            })
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(MaxUploadBytes))
            .Accepts<IFormFile>("multipart/form-data")
            .WithSummary("Recognize speech in a 16 kHz mono WAV recording (multipart field 'audio', optional 'language') – audio is not stored")
            .Produces<TranscriptionDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }
}
