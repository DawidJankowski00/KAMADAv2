using System.ComponentModel.DataAnnotations;

namespace Kamada.Shared.Infrastructure.Email;

/// <summary>How the SMTP connection is secured (MailKit's <c>SecureSocketOptions</c> without the guessing mode).</summary>
public enum SmtpSecurity
{
    /// <summary>Plain text; only for a local catcher such as Mailpit.</summary>
    None,

    /// <summary>Upgrade with STARTTLS (usually port 587); the connection fails if the server does not offer it.</summary>
    StartTls,

    /// <summary>TLS from the first byte (usually port 465).</summary>
    SslOnConnect,
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    [Required, EmailAddress]
    public string FromAddress { get; init; } = string.Empty;

    [Required]
    public string FromName { get; init; } = "KAMADA";

    [Required]
    public SmtpOptions Smtp { get; init; } = new();
}

public sealed class SmtpOptions
{
    [Required]
    public string Host { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; } = 587;

    public SmtpSecurity Security { get; init; } = SmtpSecurity.StartTls;

    /// <summary>Leave empty for servers without authentication (Mailpit). Secrets come from env vars / user secrets.</summary>
    public string? Username { get; init; }

    public string? Password { get; init; }

    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(15);
}
