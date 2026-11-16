using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Kamada.Shared.Infrastructure.Email;

/// <summary>
/// Sends each message over a new SMTP connection (MailKit). E-mails are rare (password resets), so there is no pooling.
/// </summary>
internal sealed partial class SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        var settings = options.Value;
        using var mime = CreateMimeMessage(settings, message);
        using var client = new SmtpClient { Timeout = (int)settings.Smtp.Timeout.TotalMilliseconds };

        await client.ConnectAsync(settings.Smtp.Host, settings.Smtp.Port, ToSocketOptions(settings.Smtp.Security), cancellationToken);
        if (!string.IsNullOrEmpty(settings.Smtp.Username))
        {
            await client.AuthenticateAsync(settings.Smtp.Username, settings.Smtp.Password ?? string.Empty, cancellationToken);
        }

        await client.SendAsync(mime, cancellationToken);
        await client.DisconnectAsync(quit: true, cancellationToken);
        LogSent(logger, message.Subject);
    }

    internal static MimeMessage CreateMimeMessage(EmailOptions settings, EmailMessage message)
    {
        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(settings.FromName, settings.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { TextBody = message.TextBody, HtmlBody = message.HtmlBody }.ToMessageBody();
        return mime;
    }

    internal static SecureSocketOptions ToSocketOptions(SmtpSecurity security) => security switch
    {
        SmtpSecurity.None => SecureSocketOptions.None,
        SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
        SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        _ => throw new ArgumentOutOfRangeException(nameof(security), security, null),
    };

    // The recipient is personal data: only the subject is logged.
    [LoggerMessage(Level = LogLevel.Information, Message = "E-mail sent: {Subject}")]
    private static partial void LogSent(ILogger logger, string subject);
}
