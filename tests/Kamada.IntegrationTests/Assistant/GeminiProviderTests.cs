using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Assistant.Infrastructure.Gemini;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Assistant;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kamada.IntegrationTests.Assistant;

/// <summary>
/// A developer switches the assistant to Gemini and the next requests use it, through the real client and the
/// resilience pipeline; only the HTTP endpoint is fake. Own database, because the settings are global.
/// </summary>
public sealed class GeminiProviderTests : IAsyncDisposable
{
    private const string GeminiModel = "gemini-test-model";

    private readonly KamadaApiFactory _factory;
    private readonly FakeGeminiApi _gemini = new();
    private readonly WebApplicationFactory<Program> _host;

    public GeminiProviderTests(KamadaApiFactory factory)
    {
        _factory = factory;
        _host = factory.WithOwnDatabase("kamada_assistant_gemini").WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Assistant:Gemini:ApiKey", FakeGeminiApi.ApiKey);
            builder.ConfigureTestServices(services =>
                services.AddHttpClient(GeminiClient.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => _gemini));
        });
    }

    [Fact]
    public async Task Assistant_AfterSwitchingToGemini_DraftsAndTranscribesWithGemini()
    {
        var developer = await SignInAsync();
        var ct = TestContext.Current.CancellationToken;

        var saved = await developer.PutJsonAsync("/api/assistant/settings", new UpdateAssistantSettingsRequest(
            true, AssistantTextProvider.Gemini, "gemma3:4b", GeminiModel, AssistantSpeechProvider.Gemini));
        var status = await (await developer.GetAsync("/api/assistant/status", ct)).ReadAsAsync<AssistantStatusDto>();
        var draft = await developer.PostJsonAsync("/api/assistant/order-drafts", new OrderDraftRequest("Dwie kawy dla Jana Kowalskiego", "pl"));
        var transcription = await developer.PostAsync(
            "/api/assistant/transcriptions", TranscriptionTests.Upload(TranscriptionTests.Wav(seconds: 2), "pl"), ct);

        saved.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await saved.ReadAsAsync<AssistantSettingsDto>()).Availability.GeminiConfigured.ShouldBeTrue();
        status.ShouldBe(new AssistantStatusDto(true, true, GeminiModel, AssistantTextProvider.Gemini, AssistantSpeechProvider.Gemini));
        draft.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await draft.ReadAsAsync<OrderDraftDto>()).Lines.ShouldHaveSingleItem().Quantity.ShouldBe(2);
        (await transcription.ReadAsAsync<TranscriptionDto>()).Text.ShouldBe(FakeGeminiApi.Transcript);
        _gemini.Paths.ShouldAllBe(path => path == $"/v1beta/models/{GeminiModel}:generateContent");
        _gemini.Paths.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Health_WithGeminiSelected_ProbesTheGeminiModel()
    {
        var developer = await SignInAsync();
        await developer.PutJsonAsync("/api/assistant/settings", new UpdateAssistantSettingsRequest(
            true, AssistantTextProvider.Gemini, "gemma3:4b", GeminiModel, AssistantSpeechProvider.Off));

        var report = await _host.Services.GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(check => check.Name == "assistant", TestContext.Current.CancellationToken);

        var entry = report.Entries["assistant"];
        entry.Status.ShouldBe(HealthStatus.Healthy);
        entry.Description.ShouldBe($"Gemini {GeminiModel}");
        _gemini.Paths.ShouldContain($"/v1beta/models/{GeminiModel}");
    }

    public async ValueTask DisposeAsync()
    {
        await _host.DisposeAsync();
        _gemini.Dispose();
    }

    private async Task<HttpClient> SignInAsync()
    {
        var worker = await _factory.CreateWorkerAsync([WorkerRole.Admin, WorkerRole.Developer]);
        var tokens = await _factory.CreateClient().LoginAsync(worker.Username, worker.Password);
        return _host.CreateClient().WithBearer(tokens.AccessToken);
    }
}
