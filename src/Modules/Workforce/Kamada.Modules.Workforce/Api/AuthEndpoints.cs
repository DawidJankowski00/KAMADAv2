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
            .WithSummary("Sign in with username and password (an account with 2FA gets a challenge instead of tokens)")
            .Produces<LoginResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapPost("/login/two-factor", async (TwoFactorLoginRequest request, TwoFactorLoginHandler handler, HttpContext http, CancellationToken ct) =>
                (await handler.HandleAsync(request, http.ToSessionClient(), ct)).ToHttpResult(TypedResults.Ok))
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Login)
            .WithValidation<TwoFactorLoginRequest>()
            .WithSummary("Finish a sign-in with 2FA: the challenge from /login plus an authenticator or recovery code")
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

        group.MapGet("/sessions", async (AccountSessions sessions, ICurrentUser user, CancellationToken ct) =>
                TypedResults.Ok(await sessions.ListAsync(user.Id, user.SessionId, ct)))
            .WithSummary("Own sessions (signed-in devices), the current one first");

        group.MapDelete("/sessions/{id:guid}", async (Guid id, AccountSessions sessions, ICurrentUser user, CancellationToken ct) =>
                (await sessions.RevokeAsync(user.Id, id, ct)).ToHttpResult(TypedResults.NoContent))
            .WithSummary("Sign out one of the own sessions")
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/sessions/revoke-others", async (AccountSessions sessions, ICurrentUser user, CancellationToken ct) =>
            {
                await sessions.RevokeOthersAsync(user.Id, user.SessionId, ct);
                return TypedResults.NoContent();
            })
            .WithSummary("Sign out every own session except the current one");

        group.MapGet("/two-factor", async (TwoFactorSetup setup, ICurrentUser user, CancellationToken ct) =>
                (await setup.GetStatusAsync(user.Id, ct)).ToHttpResult(TypedResults.Ok))
            .WithSummary("Whether 2FA is on and how many recovery codes are left");

        group.MapPost("/two-factor/setup", async (TwoFactorSetup setup, ICurrentUser user, CancellationToken ct) =>
                (await setup.BeginAsync(user.Id, ct)).ToHttpResult(TypedResults.Ok))
            .WithSummary("Create a secret for the authenticator app (2FA stays off until enabled with a code)")
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/two-factor/enable", async (TwoFactorCodeRequest request, TwoFactorSetup setup, ICurrentUser user, CancellationToken ct) =>
                (await setup.EnableAsync(user.Id, request, ct)).ToHttpResult(TypedResults.Ok))
            .RequireRateLimiting(RateLimitPolicies.Login)
            .WithValidation<TwoFactorCodeRequest>()
            .WithSummary("Turn 2FA on with a code from the app; returns the recovery codes (shown once)")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/two-factor/disable", async (DisableTwoFactorRequest request, TwoFactorSetup setup, ICurrentUser user, CancellationToken ct) =>
                (await setup.DisableAsync(user.Id, request, ct)).ToHttpResult(TypedResults.NoContent))
            .RequireRateLimiting(RateLimitPolicies.Login)
            .WithValidation<DisableTwoFactorRequest>()
            .WithSummary("Turn 2FA off (needs the password and a code)")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/two-factor/recovery-codes", async (TwoFactorCodeRequest request, TwoFactorSetup setup, ICurrentUser user, CancellationToken ct) =>
                (await setup.RegenerateRecoveryCodesAsync(user.Id, request, ct)).ToHttpResult(TypedResults.Ok))
            .RequireRateLimiting(RateLimitPolicies.Login)
            .WithValidation<TwoFactorCodeRequest>()
            .WithSummary("Replace the recovery codes (needs a code)")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/me", async (IWorkforceDbContext db, TwoFactorPolicy twoFactorPolicy, ICurrentUser user, CancellationToken ct) =>
            {
                var worker = await db.Workers.AsNoTracking().SingleOrDefaultAsync(w => w.Id == user.Id, ct);
                return worker is null
                    ? WorkerErrors.NotFound.ToProblem()
                    : Results.Ok(worker.ToAuthenticatedUser(twoFactorPolicy.SetupRequired(worker)));
            })
            .WithSummary("Currently signed-in worker")
            .Produces<AuthenticatedUserDto>();

        return endpoints;
    }
}
