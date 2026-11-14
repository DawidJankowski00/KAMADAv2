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
}
