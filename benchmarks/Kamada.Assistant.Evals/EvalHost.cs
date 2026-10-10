using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace Kamada.Assistant.Evals;

/// <summary>
/// The whole API in-process against a dedicated PostgreSQL database seeded with the demo data (created on the first
/// run), with the real Ollama model. Speech recognition is off (the evals measure text understanding), geocoding is
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
        builder.UseSetting("Geocoding:Provider", options.Geocode ? "Nominatim" : "None");
        builder.UseSetting("RouteOptimization:MatrixProvider", "Haversine");
        builder.UseSetting("RateLimiting:Login:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Assistant:PermitLimit", "100000");
        builder.UseSetting("Assistant:Enabled", "true");
        builder.UseSetting("Assistant:Ollama:BaseUrl", options.OllamaUrl.ToString());
        builder.UseSetting("Assistant:Ollama:Model", options.Model);
        builder.UseSetting("Assistant:Ollama:RequestTimeout", "00:05:00");
        builder.UseSetting("Assistant:Whisper:Enabled", "false");
        if (options.NoThink)
        {
            builder.UseSetting("Assistant:Ollama:Think", "false");
        }

        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");
    }
}
