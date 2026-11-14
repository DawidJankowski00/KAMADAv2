using Kamada.Shared.Infrastructure.Email;
using MailKit.Security;
using Shouldly;

namespace Kamada.Shared.UnitTests;

public sealed class SmtpEmailSenderTests
{
    private static readonly EmailOptions Options = new()
    {
        FromAddress = "no-reply@kamada.test",
        FromName = "KAMADA",
        Smtp = new SmtpOptions { Host = "localhost" },
    };

    [Fact]
    public void CreateMimeMessage_SetsSenderRecipientSubjectAndBothBodies()
    {
        using var mime = SmtpEmailSender.CreateMimeMessage(
            Options,
            new EmailMessage("anna@kamada.test", "Reset hasła", "Tekst", "<p>HTML</p>"));

        mime.From.Mailboxes.Single().Address.ShouldBe("no-reply@kamada.test");
        mime.From.Mailboxes.Single().Name.ShouldBe("KAMADA");
        mime.To.Mailboxes.Single().Address.ShouldBe("anna@kamada.test");
        mime.Subject.ShouldBe("Reset hasła");
        mime.TextBody.ShouldBe("Tekst");
        mime.HtmlBody.ShouldBe("<p>HTML</p>");
    }

    [Fact]
    public void CreateMimeMessage_WithoutHtml_SendsTextOnly()
    {
        using var mime = SmtpEmailSender.CreateMimeMessage(Options, new EmailMessage("anna@kamada.test", "Temat", "Tekst"));

        mime.TextBody.ShouldBe("Tekst");
        mime.HtmlBody.ShouldBeNull();
    }

    [Theory]
    [InlineData(nameof(SmtpSecurity.None), SecureSocketOptions.None)]
    [InlineData(nameof(SmtpSecurity.StartTls), SecureSocketOptions.StartTls)]
    [InlineData(nameof(SmtpSecurity.SslOnConnect), SecureSocketOptions.SslOnConnect)]
    public void ToSocketOptions_MapsEverySecurityMode(string security, SecureSocketOptions expected)
    {
        SmtpEmailSender.ToSocketOptions(Enum.Parse<SmtpSecurity>(security)).ShouldBe(expected);
    }
}
