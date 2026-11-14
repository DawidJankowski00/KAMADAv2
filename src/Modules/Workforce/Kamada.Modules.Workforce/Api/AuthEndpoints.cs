using Kamada.Modules.Workforce.Application;
using Kamada.Modules.Workforce.Application.Abstractions;
using Kamada.Modules.Workforce.Application.Auth;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Auth;
using Kamada.Shared.Infrastructure.Auth;
using Kamada.Shared.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Workforce.Api;

internal static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/login", async (LoginRequest request, LoginHandler handler, HttpContext http, CancellationToken ct) =>
                (await handler.HandleAsync(request, http.ToSessionClient(), ct)).ToHttpResult(TypedResults.Ok))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Login)
            .WithValidation<LoginRequest>()
            .WithSummary("Sign in with username and password")
            .Produces<TokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/refresh", async (RefreshTokenRequest request, RefreshTokenHandler handler, HttpContext http, CancellationToken ct) =>
                (await handler.HandleAsync(request, http.ToSessionClient(), ct)).ToHttpResult(TypedResults.Ok))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Login)
            .WithValidation<RefreshTokenRequest>()
            .WithSummary("Exchange a refresh token for a new token pair (rotation)")
            .Produces<TokenResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", async (LogoutRequest request, LogoutHandler handler, ICurrentUser user, CancellationToken ct) =>
            {
                await handler.HandleAsync(user.Id, request, ct);
                return TypedResults.NoContent();
            })
            .WithValidation<LogoutRequest>()
            .WithSummary("Revoke a refresh token");

        group.MapPost("/change-password", async (ChangePasswordRequest request, ChangePasswordHandler handler, ICurrentUser user, HttpContext http, CancellationToken ct) =>
                (await handler.HandleAsync(user.Id, request, http.ToSessionClient(), ct)).ToHttpResult(TypedResults.Ok))
            .RequireRateLimiting(RateLimitPolicies.Login)
            .WithValidation<ChangePasswordRequest>()
            .WithSummary("Change own password (signs out every other session, returns a new token pair)")
            .Produces<TokenResponse>()
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/forgot-password", async (ForgotPasswordRequest request, ForgotPasswordHandler handler, CancellationToken ct) =>
            {
                await handler.HandleAsync(request, ct);
                return TypedResults.Accepted((string?)null);
            })
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Login)
            .WithValidation<ForgotPasswordRequest>()
            .WithSummary("Send a password reset link (always 202, whether or not the account exists)")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/reset-password", async (CompletePasswordResetRequest request, ResetPasswordHandler handler, CancellationToken ct) =>
                (await handler.HandleAsync(request, ct)).ToHttpResult(TypedResults.NoContent))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Login)
            .WithValidation<CompletePasswordResetRequest>()
            .WithSummary("Set a new password with the token from the reset e-mail (signs out every session)")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapGet("/me", async (IWorkforceDbContext db, ICurrentUser user, CancellationToken ct) =>
            {
                var worker = await db.Workers.AsNoTracking().SingleOrDefaultAsync(w => w.Id == user.Id, ct);
                return worker is null
                    ? WorkerErrors.NotFound.ToProblem()
                    : Results.Ok(worker.ToAuthenticatedUser());
            })
            .WithSummary("Currently signed-in worker")
            .Produces<AuthenticatedUserDto>();

        return endpoints;
    }
}
