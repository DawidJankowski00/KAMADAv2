using Kamada.Shared.Contracts.Auth;

namespace Kamada.Web.Shared.Auth;

/// <summary>Result of the password step: signed in (<see cref="User"/>), or a code is needed (<see cref="TwoFactor"/>).</summary>
public sealed record LoginStep(AuthenticatedUserDto? User, TwoFactorChallengeDto? TwoFactor)
{
    public bool NeedsCode => TwoFactor is not null;
}
