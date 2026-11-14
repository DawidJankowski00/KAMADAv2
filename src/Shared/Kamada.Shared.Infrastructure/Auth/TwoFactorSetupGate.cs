using Kamada.Shared.Kernel.Results;
using Kamada.Shared.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace Kamada.Shared.Infrastructure.Auth;

/// <summary>
/// A worker whose access token carries the <c>2fa_setup</c> claim (an administrator without two-factor
/// authentication where it is required) may call only <c>/api/auth/*</c> – the account endpoints that set 2FA up,
/// sessions and sign-out. Everything else answers 403 <c>Auth.TwoFactorSetupRequired</c>, so the rule holds for every
/// client, not only for the apps that show the setup page.
/// </summary>
public static class TwoFactorSetupGate
{
    public static readonly Error SetupRequired =
        Error.Forbidden("Auth.TwoFactorSetupRequired", "Set up two-factor authentication to continue.");

    /// <summary>Registers the gate; must come after <c>UseAuthentication</c>.</summary>
    public static IApplicationBuilder UseTwoFactorSetupGate(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.User.HasClaim(KamadaClaimTypes.TwoFactorSetupRequired, "true")
                && context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase)
                && !context.Request.Path.StartsWithSegments("/api/auth", StringComparison.OrdinalIgnoreCase))
            {
                await SetupRequired.ToProblem().ExecuteAsync(context);
                return;
            }

            await next(context);
        });
}
