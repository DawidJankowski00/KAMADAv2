using Kamada.Modules.Assistant.Application;
using Kamada.Modules.Assistant.Application.Drafts;
using Kamada.Shared.Contracts.Assistant;
using Kamada.Shared.Infrastructure.Auth;
using Kamada.Shared.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.Assistant.Api;

internal static class AssistantEndpoints
{
    public static IEndpointRouteBuilder MapOrderAssistantEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/assistant")
            .WithTags("Assistant")
            .RequireAuthorization(AuthorizationPolicies.AdminOnly);

        group.MapGet("/status", (AssistantStatusQuery query) => TypedResults.Ok(query.Get()))
            .WithSummary("Whether the order assistant (and dictation) is available and which model it uses")
            .Produces<AssistantStatusDto>();

        // Inference is expensive: these endpoints are rate-limited and answer 503 when the feature is off.
        var inference = group.MapGroup("/")
            .RequireRateLimiting(RateLimitPolicies.Assistant)
            .AddEndpointFilter(async (context, next) =>
                context.HttpContext.RequestServices.GetRequiredService<IOptions<AssistantOptions>>().Value.Enabled
                    ? await next(context)
                    : AssistantErrors.Disabled.ToProblem())
            .ProducesProblem(StatusCodes.Status429TooManyRequests)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        inference.MapPost("/order-drafts", async (OrderDraftRequest request, OrderDraftHandler handler, CancellationToken ct) =>
                (await handler.CreateAsync(request, ct)).ToHttpResult(TypedResults.Ok))
            .WithValidation<OrderDraftRequest>()
            .WithSummary("Turn a dictated or typed note into an order draft (client, address, products) – nothing is saved")
            .Produces<OrderDraftDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }
}
