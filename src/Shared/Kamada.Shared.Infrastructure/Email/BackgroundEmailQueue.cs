using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kamada.Shared.Infrastructure.Email;

/// <summary>In-memory queue drained by a hosted service that sends through <see cref="IEmailSender"/>.</summary>
internal sealed partial class BackgroundEmailQueue(IEmailSender sender, ILogger<BackgroundEmailQueue> logger)
    : BackgroundService, IEmailQueue
{
    // Bounded: a flood of reset requests must not grow memory without limit; the oldest waiting e-mail is dropped.
    private readonly Channel<EmailMessage> _queue = Channel.CreateBounded<EmailMessage>(
        new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    public void Enqueue(EmailMessage message)
    {
        if (!_queue.Writer.TryWrite(message))
        {
            LogDropped(logger, message.Subject);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await sender.SendAsync(message, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // One failed e-mail must not stop the queue; the user can ask again.
            catch (Exception exception)
#pragma warning restore CA1031
            {
                LogFailed(logger, exception, message.Subject);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Sending the e-mail '{Subject}' failed")]
    private static partial void LogFailed(ILogger logger, Exception exception, string subject);

    [LoggerMessage(Level = LogLevel.Warning, Message = "E-mail queue is closed; '{Subject}' was dropped")]
    private static partial void LogDropped(ILogger logger, string subject);
}
