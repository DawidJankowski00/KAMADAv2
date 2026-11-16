namespace Kamada.Shared.Infrastructure.Email;

/// <summary>A plain e-mail to one recipient. <paramref name="HtmlBody"/> is optional; the text body is always sent.</summary>
public sealed record EmailMessage(string To, string Subject, string TextBody, string? HtmlBody = null);
