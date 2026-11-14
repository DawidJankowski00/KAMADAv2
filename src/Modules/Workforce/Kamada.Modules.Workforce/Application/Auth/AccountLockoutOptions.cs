using System.ComponentModel.DataAnnotations;

namespace Kamada.Modules.Workforce.Application.Auth;

/// <summary>Section <c>Auth:Lockout</c>: how many wrong passwords lock an account, and for how long.</summary>
internal sealed class AccountLockoutOptions
{
    public const string SectionName = "Auth:Lockout";

    [Range(3, 100)]
    public int MaxFailedAttempts { get; init; } = 5;

    [Range(typeof(TimeSpan), "00:01:00", "1.00:00:00")]
    public TimeSpan Duration { get; init; } = TimeSpan.FromMinutes(15);
}
