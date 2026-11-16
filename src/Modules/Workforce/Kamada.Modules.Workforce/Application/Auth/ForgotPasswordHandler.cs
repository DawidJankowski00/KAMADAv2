using System.Globalization;
using Kamada.Modules.Workforce.Application.Abstractions;
using Kamada.Modules.Workforce.Domain.Auth;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Auth;
using Kamada.Shared.Infrastructure.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.Workforce.Application.Auth;

/// <summary>
/// Sends a password reset link to an active worker with the given e-mail. The caller always gets the same answer, and
/// the e-mail goes through a background queue, so neither the response nor its timing tells whether the account exists.
/// </summary>
internal sealed partial class ForgotPasswordHandler(
    IWorkforceDbContext db,
    ITokenService tokens,
    IEmailQueue emails,
    IOptions<PasswordResetOptions> options,
    TimeProvider clock,
    ILogger<ForgotPasswordHandler> logger)
{
    public async Task HandleAsync(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var email = Worker.NormalizeEmail(request.Email);
        var worker = await db.Workers.SingleOrDefaultAsync(w => w.Email == email && w.IsActive, cancellationToken);
        if (worker is null)
        {
            LogUnknownEmail();
            return;
        }

        var now = clock.GetUtcNow();
        var pending = await db.PasswordResetTokens
            .Where(t => t.WorkerId == worker.Id && t.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var older in pending)
        {
            older.Invalidate(now);
        }

        var generated = tokens.GenerateOpaqueToken();
        var settings = options.Value;
        db.PasswordResetTokens.Add(PasswordResetToken.Issue(worker.Id, generated.Hash, now, settings.TokenLifetime));
        await db.SaveChangesAsync(cancellationToken);

        emails.Enqueue(CreateEmail(worker, ResetLink(settings.Url!, generated.Token), settings.TokenLifetime));
        LogResetRequested(worker.Id);
    }

    internal static Uri ResetLink(Uri page, string token) =>
        new UriBuilder(page) { Query = "token=" + Uri.EscapeDataString(token) }.Uri;

    private static EmailMessage CreateEmail(Worker worker, Uri link, TimeSpan lifetime)
    {
        var minutes = ((int)lifetime.TotalMinutes).ToString(CultureInfo.InvariantCulture);
        var text = $"""
            Dzień dobry {worker.Name},

            otrzymaliśmy prośbę o ustawienie nowego hasła do konta {worker.Username} w systemie KAMADA.
            Aby ustawić nowe hasło, otwórz link (ważny {minutes} minut, działa jeden raz):

            {link.AbsoluteUri}

            Jeśli to nie Ty prosiłeś(-aś) o zmianę, zignoruj tę wiadomość – hasło pozostanie bez zmian.
            """;
        return new EmailMessage(worker.Email, "KAMADA – ustawienie nowego hasła", text);
    }

    // The e-mail address is personal data and would let log readers probe accounts: it is not logged.
    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset requested for an unknown or inactive e-mail address")]
    private partial void LogUnknownEmail();

    [LoggerMessage(Level = LogLevel.Information, Message = "Password reset link sent to worker {WorkerId}")]
    private partial void LogResetRequested(Guid workerId);
}
