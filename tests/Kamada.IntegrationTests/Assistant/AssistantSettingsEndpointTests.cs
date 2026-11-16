using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Assistant;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kamada.IntegrationTests.Assistant;

/// <summary>
/// <c>/api/assistant/settings</c>: developers only, saved settings take effect at once. Own database, because the
/// settings are global; tokens come from the shared host (same signing key).
/// </summary>
public sealed class AssistantSettingsEndpointTests(KamadaApiFactory factory) : IAsyncDisposable
{
    private const string Url = "/api/assistant/settings";

    private readonly WebApplicationFactory<Program> _host = factory.WithOwnDatabase("kamada_assistant_settings");

    [Theory]
    [InlineData(nameof(WorkerRole.Admin))]
    [InlineData(nameof(WorkerRole.Seller))]
    [InlineData(nameof(WorkerRole.Courier))]
    public async Task Get_WithoutDeveloperRole_Returns403(string role)
    {
        var client = await SignInAsync(Enum.Parse<WorkerRole>(role));

        var response = await client.GetAsync(Url, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Put_WithoutDeveloperRole_Returns403()
    {
        var admin = await SignInAsync(WorkerRole.Admin);

        var response = await admin.PutJsonAsync(Url, Request("qwen3:4b"));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_Developer_ReturnsSettingsAndWhatTheServerAllows()
    {
        var developer = await SignInAsync(WorkerRole.Developer);

        var settings = await (await developer.GetAsync(Url, TestContext.Current.CancellationToken)).ReadAsAsync<AssistantSettingsDto>();

        settings.Enabled.ShouldBeTrue();
        settings.TextProvider.ShouldBe(AssistantTextProvider.Ollama);
        settings.SpeechProvider.ShouldBe(AssistantSpeechProvider.Whisper);
        settings.GeminiModel.ShouldNotBeNullOrWhiteSpace();
        settings.Availability.AssistantEnabled.ShouldBeTrue();
    }

    [Fact]
    public async Task Put_Developer_SavesAndTheAssistantUsesItAtOnce()
    {
        var developer = await SignInAsync(WorkerRole.Admin, WorkerRole.Developer);

        var response = await developer.PutJsonAsync(Url, Request("qwen3:4b", speech: AssistantSpeechProvider.Off));
        var saved = await response.ReadAsAsync<AssistantSettingsDto>();
        var status = await (await developer.GetAsync("/api/assistant/status", TestContext.Current.CancellationToken)).ReadAsAsync<AssistantStatusDto>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        saved.OllamaModel.ShouldBe("qwen3:4b");
        saved.SpeechProvider.ShouldBe(AssistantSpeechProvider.Off);
        saved.UpdatedAt.ShouldNotBeNull();
        status.Model.ShouldBe("qwen3:4b");
        status.SpeechToText.ShouldBeFalse();
        (await developer.PutJsonAsync(Url, Request("gemma3:4b"))).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Theory]
    [InlineData("")]
    [InlineData("gemma3:4b; rm -rf")]
    [InlineData("-leading-dash")]
    public async Task Put_InvalidModel_Returns400(string model)
    {
        var developer = await SignInAsync(WorkerRole.Developer);

        var response = await developer.PutJsonAsync(Url, Request(model));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData(nameof(AssistantTextProvider.Gemini), nameof(AssistantSpeechProvider.Whisper))]
    [InlineData(nameof(AssistantTextProvider.Ollama), nameof(AssistantSpeechProvider.Gemini))]
    public async Task Put_GeminiWithoutApiKey_Returns400(string text, string speech)
    {
        var developer = await SignInAsync(WorkerRole.Developer);
        var request = new UpdateAssistantSettingsRequest(
            true, Enum.Parse<AssistantTextProvider>(text), "gemma3:4b", "gemini-3.5-flash-lite", Enum.Parse<AssistantSpeechProvider>(speech));

        var settings = await (await developer.GetAsync(Url, TestContext.Current.CancellationToken)).ReadAsAsync<AssistantSettingsDto>();
        var response = await developer.PutJsonAsync(Url, request);

        settings.Availability.GeminiConfigured.ShouldBeFalse();
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemCodeAsync()).ShouldBe("Assistant.GeminiNotConfigured");
    }

    [Fact]
    public async Task TestConnection_LocalProviders_ReportsEachWithItsModel()
    {
        var developer = await SignInAsync(WorkerRole.Developer);

        var response = await developer.PostAsync($"{Url}/test", null, TestContext.Current.CancellationToken);
        var result = await response.ReadAsAsync<AssistantProviderTestDto>();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        result.Text.Provider.ShouldBe("Ollama");
        result.Text.Model.ShouldBe("gemma3:4b");
        // The fake extraction knows only the notes tests register: the sample note reaches it and is "not understood".
        result.Text.ErrorCode.ShouldBe("Assistant.ExtractionFailed");
        result.Speech.ShouldBe(result.Speech! with { Provider = "Whisper", Success = true, ErrorCode = null });
    }

    [Fact]
    public async Task TestConnection_WithoutDeveloperRole_Returns403()
    {
        var admin = await SignInAsync(WorkerRole.Admin);

        var response = await admin.PostAsync($"{Url}/test", null, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();

    private static UpdateAssistantSettingsRequest Request(
        string ollamaModel, AssistantSpeechProvider speech = AssistantSpeechProvider.Whisper) =>
        new(true, AssistantTextProvider.Ollama, ollamaModel, "gemini-3.5-flash-lite", speech);

    private async Task<HttpClient> SignInAsync(params WorkerRole[] roles)
    {
        var worker = await factory.CreateWorkerAsync(roles);
        var tokens = await factory.CreateClient().LoginAsync(worker.Username, worker.Password);
        return _host.CreateClient().WithBearer(tokens.AccessToken);
    }
}
