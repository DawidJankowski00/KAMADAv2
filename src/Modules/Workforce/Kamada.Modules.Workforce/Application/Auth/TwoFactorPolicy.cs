using Kamada.Modules.Workforce.Domain.Workers;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.Workforce.Application.Auth;

/// <summary>Section <c>Auth:TwoFactor</c>.</summary>
internal sealed class TwoFactorPolicyOptions
{
    public const string SectionName = "Auth:TwoFactor";

    /// <summary>
    /// Administrators must turn 2FA on before they can use anything but their account pages. On by default; the
    /// Development configuration turns it off so the demo accounts sign in with the password only.
    /// </summary>
    public bool RequiredForAdmins { get; init; } = true;
}

internal sealed class TwoFactorPolicy(IOptions<TwoFactorPolicyOptions> options)
{
    /// <summary>The worker must set 2FA up first; their tokens then carry the <c>2fa_setup</c> claim.</summary>
    public bool SetupRequired(Worker worker) =>
        options.Value.RequiredForAdmins && worker.HasRole(WorkerRole.Admin) && !worker.TwoFactorEnabled;
}
