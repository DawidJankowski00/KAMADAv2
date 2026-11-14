namespace Kamada.Shared.Infrastructure.Email;

/// <summary>
/// Sends e-mails (password reset links). Failures throw: the caller decides whether a lost e-mail fails its request.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
