namespace Kamada.Shared.Infrastructure.Email;

/// <summary>
/// Hands an e-mail to a background sender, so a request neither waits for SMTP nor fails or slows down when it is
/// unavailable (which would also tell an attacker which accounts exist). Lost on restart: only for e-mails a user can
/// ask for again, such as password reset links.
/// </summary>
public interface IEmailQueue
{
    void Enqueue(EmailMessage message);
}
