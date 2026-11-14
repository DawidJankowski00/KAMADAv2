using System.Collections.Concurrent;
using Kamada.Shared.Infrastructure.Email;

namespace Kamada.IntegrationTests.Infrastructure;

/// <summary>
/// Records e-mails instead of sending them. One instance serves all tests in the assembly, so tests look up
/// messages by their own (unique) recipient.
/// </summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyCollection<EmailMessage> Sent => _sent;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public IReadOnlyList<EmailMessage> SentTo(string recipient) =>
        [.. _sent.Where(m => string.Equals(m.To, recipient, StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// Waits for the <paramref name="count"/>-th e-mail to the recipient: the API sends from a background queue, so it
    /// may arrive shortly after the response.
    /// </summary>
    public async Task<EmailMessage> WaitForAsync(string recipient, int count = 1)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var sent = SentTo(recipient);
            if (sent.Count >= count)
            {
                return sent[count - 1];
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"No e-mail #{count} to {recipient} within 10 s.");
    }
}
