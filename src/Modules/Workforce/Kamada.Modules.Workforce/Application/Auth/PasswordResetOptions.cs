using System.ComponentModel.DataAnnotations;

namespace Kamada.Modules.Workforce.Application.Auth;

/// <summary>Section <c>Auth:PasswordReset</c>.</summary>
internal sealed class PasswordResetOptions
{
    public const string SectionName = "Auth:PasswordReset";

    /// <summary>Page of the web panel that sets the new password; the e-mail link adds <c>?token=…</c>.</summary>
    [Required]
    public Uri? Url { get; init; }

    [Range(typeof(TimeSpan), "00:05:00", "1.00:00:00")]
    public TimeSpan TokenLifetime { get; init; } = TimeSpan.FromMinutes(30);
}
