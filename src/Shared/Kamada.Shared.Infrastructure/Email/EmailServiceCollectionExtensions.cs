using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.Shared.Infrastructure.Email;

public static class EmailServiceCollectionExtensions
{
    /// <summary>Registers <see cref="IEmailSender"/> over SMTP and the background <see cref="IEmailQueue"/>, with options from the <c>Email</c> section.</summary>
    public static IServiceCollection AddKamadaEmail(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.Smtp is not null && !string.IsNullOrWhiteSpace(o.Smtp.Host), "Email:Smtp:Host is required.")
            .ValidateOnStart();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<BackgroundEmailQueue>();
        services.AddSingleton<IEmailQueue>(sp => sp.GetRequiredService<BackgroundEmailQueue>());
        services.AddHostedService(sp => sp.GetRequiredService<BackgroundEmailQueue>());
        return services;
    }
}
