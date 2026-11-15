using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Domain.Settings;
using Kamada.Modules.Assistant.Infrastructure.Persistence;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Assistant;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.IntegrationTests.Assistant;

/// <summary>
/// The assistant reads its developer settings from the database at run time (own database: the settings are global).
/// Access tokens are issued by the shared host; the hosts share the signing key, so they are valid here too.
/// </summary>
public sealed class AssistantRuntimeSettingsTests(KamadaApiFactory factory) : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _host = factory.WithOwnDatabase("kamada_assistant_runtime");

    [Fact]
    public async Task Status_FirstRead_StartsFromTheConfiguration()
    {
        var admin = await SignInAsync();

        var status = await GetStatusAsync(admin);

        status.Enabled.ShouldBeTrue();
        status.Model.ShouldBe("gemma3:4b");
        await using var scope = _host.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AssistantDbContext>().Settings.SingleAsync(TestContext.Current.CancellationToken);
        stored.TextProvider.ShouldBe(TextProvider.Ollama);
        stored.SpeechProvider.ShouldBe(SpeechProvider.Whisper);
    }

    [Fact]
    public async Task Status_AfterStoredSettingsChange_ReportsThemAndSwitchedOffAssistantAnswers503()
    {
        var admin = await SignInAsync();
        await GetStatusAsync(admin);

        await UpdateStoredAsync(enabled: true, ollamaModel: "qwen3:4b");
        (await GetStatusAsync(admin)).Model.ShouldBe("qwen3:4b");

        await UpdateStoredAsync(enabled: false, ollamaModel: "gemma3:4b");
        var response = await admin.PostJsonAsync("/api/assistant/order-drafts", new OrderDraftRequest("dwie kawy"));

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.ReadProblemCodeAsync()).ShouldBe("Assistant.Disabled");
        (await GetStatusAsync(admin)).Enabled.ShouldBeFalse();
        await UpdateStoredAsync(enabled: true, ollamaModel: "gemma3:4b");
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    private async Task<HttpClient> SignInAsync()
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Admin);
        var tokens = await factory.CreateClient().LoginAsync(worker.Username, worker.Password);
        return _host.CreateClient().WithBearer(tokens.AccessToken);
    }

    private static async Task<AssistantStatusDto> GetStatusAsync(HttpClient client) =>
        await (await client.GetAsync("/api/assistant/status", TestContext.Current.CancellationToken)).ReadAsAsync<AssistantStatusDto>();

    private async Task UpdateStoredAsync(bool enabled, string ollamaModel)
    {
        await using var scope = _host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AssistantDbContext>();
        var settings = await db.Settings.SingleAsync(TestContext.Current.CancellationToken);
        settings.Update(enabled, TextProvider.Ollama, ollamaModel, settings.GeminiModel, SpeechProvider.Whisper, Guid.CreateVersion7()).IsSuccess.ShouldBeTrue();
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        scope.ServiceProvider.GetRequiredService<IAssistantSettings>().Invalidate();
    }
}
