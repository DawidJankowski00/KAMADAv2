using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Domain.Settings;
using Kamada.Modules.Assistant.Infrastructure.Persistence;

namespace Kamada.Assistant.Evals;

/// <summary>
/// The whole API in-process against a dedicated PostgreSQL database seeded with the demo data (created on the first
/// run), with the real model (Ollama or Gemini). Speech recognition is off (the evals measure text understanding), geocoding is
/// off unless <c>--geocode</c> (address decisions do not depend on it and it keeps runs fast and repeatable).
/// </summary>
internal sealed class EvalHost(EvalOptions options) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // The default content-root lookup searches for a *.sln file; this repository has Kamada.slnx.
        builder.UseSolutionRelativeContentRoot("src/Kamada.Api", AppContext.BaseDirectory, "Kamada.slnx");
        builder.UseEnvironment("Evals");
        builder.UseSetting("ConnectionStrings:Kamada", options.ConnectionString);
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Database:SeedDemoData", "true");
        builder.UseSetting("Jwt:SigningKey", "assistant-evals-only-signing-key-0123456789");
        builder.UseSetting("Email:Smtp:Host", "localhost"); // never used: evals send no e-mail
        builder.UseSetting("Auth:PasswordReset:Url", "http://localhost/reset-password"); // never used either
        builder.UseSetting("Geocoding:Provider", options.Geocode ? "Nominatim" : "None");
        builder.UseSetting("RouteOptimization:MatrixProvider", "Haversine");
        builder.UseSetting("RateLimiting:Login:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Assistant:PermitLimit", "100000");
        builder.UseSetting("Assistant:Enabled", "true");
        builder.UseSetting("Assistant:Ollama:BaseUrl", options.OllamaUrl.ToString());
        if (options.IsGemini)
        {
            builder.UseSetting("Assistant:Gemini:ApiKey", options.GeminiApiKey);
        }
        builder.UseSetting("Assistant:Ollama:RequestTimeout", "00:05:00");
        builder.UseSetting("Assistant:Whisper:Enabled", "false");
        if (options.NoThink)
        {
            builder.UseSetting("Assistant:Ollama:Think", "false");
        }

        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");
    }

    /// <summary>
    /// Selects the provider and model under test in the developer settings. They live in the evals database and outlast
    /// a run, so they are set explicitly every time instead of relying on the starting values from the configuration.
    /// </summary>
    public async Task SelectProviderAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var settings = scope.ServiceProvider.GetRequiredService<IAssistantSettings>();
        await settings.GetAsync(CancellationToken.None);

        var db = scope.ServiceProvider.GetRequiredService<AssistantDbContext>();
        var stored = await db.Settings.SingleAsync();
        var updated = stored.Update(
            enabled: true,
            options.IsGemini ? TextProvider.Gemini : TextProvider.Ollama,
            options.IsGemini ? stored.OllamaModel : options.Model,
            options.IsGemini ? options.Model : stored.GeminiModel,
            SpeechProvider.Off,
            updatedBy: Guid.Empty);
        if (updated.IsFailure)
        {
            throw new InvalidOperationException(updated.Error.Description);
        }

        await db.SaveChangesAsync();
        settings.Invalidate();
    }
}
