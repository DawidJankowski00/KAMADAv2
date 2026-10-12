using Kamada.Shared.Matching.Drafts;
using Microsoft.JSInterop;

namespace Kamada.Web.Shared.Assistant;

/// <summary>
/// The models running in the browser (<c>local-assistant.js</c>): downloading, loading, deleting, and the two things they do
/// – dictation to text and note to extraction JSON. One language model and one speech model are in memory at a time (both
/// only on phones with enough memory), because a phone does not have room for more.
/// </summary>
public sealed class LocalAssistantRuntime(IJSRuntime js, KamadaClientOptions options) : IAsyncDisposable
{
    public const string VendorUrl = "assistant/vendor/";

    private readonly SemaphoreSlim _lock = new(1, 1);
    private IJSObjectReference? _module;
    private DeviceCapabilities? _capabilities;

    public async Task<DeviceCapabilities> GetCapabilitiesAsync()
    {
        if (_capabilities is null)
        {
            try
            {
                _capabilities = await InvokeAsync<DeviceCapabilities>("capabilities");
            }
            catch (JSException)
            {
                return DeviceCapabilities.None;
            }
        }

        return _capabilities;
    }

    /// <summary>Refreshes the storage figures (they change after a download or a delete).</summary>
    public async Task<DeviceCapabilities> RefreshCapabilitiesAsync()
    {
        _capabilities = null;
        return await GetCapabilitiesAsync();
    }

    public Task<bool> PersistStorageAsync() => InvokeAsync<bool>("persist");

    public async Task<bool> IsInstalledAsync(LocalModel model)
    {
        try
        {
            return await InvokeAsync<bool>(model.Kind == LocalModelKind.Language ? "isLlmCached" : "isSttCached", Describe(model));
        }
        catch (JSException)
        {
            return false;
        }
    }

    /// <summary>Downloads the model (first time) and loads it; the files stay in Cache Storage afterwards.</summary>
    public async Task LoadAsync(LocalModel model, IProgress<DownloadProgress>? progress = null)
    {
        await _lock.WaitAsync();
        try
        {
            var capabilities = await GetCapabilitiesAsync();
            if (!capabilities.CanKeepBothModelsLoaded)
            {
                await InvokeAsync<object?>(model.Kind == LocalModelKind.Language ? "unloadStt" : "unloadLlm");
            }

            using var sink = DotNetObjectReference.Create(new ProgressSink(progress));
            await InvokeAsync<object?>(model.Kind == LocalModelKind.Language ? "loadLlm" : "loadStt", Describe(model), sink);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<bool> IsLoadedAsync(LocalModel model) => model.Kind == LocalModelKind.Language
        ? await InvokeAsync<string?>("loadedLlm") == model.Id
        : (await InvokeAsync<LoadedSpeech?>("loadedStt"))?.Id == model.Id;

    /// <summary>Frees the memory held by the models (the downloaded files stay).</summary>
    public Task UnloadAsync() => InvokeAsync<object?>("unloadAll");

    /// <summary>Removes the downloaded files of the model.</summary>
    public async Task DeleteAsync(LocalModel model)
    {
        await InvokeAsync<object?>(model.Kind == LocalModelKind.Language ? "deleteLlm" : "deleteStt", Describe(model));
        _capabilities = null;
    }

    /// <returns>The model's JSON answer (the language model must be loaded).</returns>
    public async Task<string> ExtractAsync(string text, AssistantLanguage language)
    {
        var messages = new List<object> { new { role = "system", content = ExtractionPrompt.System } };
        foreach (var (input, output) in ExtractionPrompt.Examples)
        {
            messages.Add(new { role = "user", content = input });
            messages.Add(new { role = "assistant", content = output });
        }

        messages.Add(new { role = "user", content = ExtractionPrompt.UserMessage(text, language) });
        return await InvokeAsync<string>("extract", messages, ExtractionPrompt.JsonSchema);
    }

    /// <param name="wav">16 kHz mono 16-bit PCM WAV.</param>
    /// <param name="language"><c>pl</c>, <c>en</c> or <c>auto</c>.</param>
    public Task<string> TranscribeAsync(byte[] wav, string language) => InvokeAsync<string>("transcribe", wav, language);

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }

        _lock.Dispose();
    }

    private static object Describe(LocalModel model) => new { id = model.Id, repo = model.Repo, contextWindow = model.ContextWindow };

    private async Task<IJSObjectReference> ModuleAsync()
    {
        if (_module is null)
        {
            _module = await js.InvokeAsync<IJSObjectReference>("import", "./_content/Kamada.Web.Shared/local-assistant.js");
            await _module.InvokeVoidAsync("configure", new { vendorUrl = VendorUrl, modelsUrl = options.AssistantModelsUrl });
        }

        return _module;
    }

    private async Task<T> InvokeAsync<T>(string function, params object?[] arguments) =>
        await (await ModuleAsync()).InvokeAsync<T>(function, arguments);

    private sealed record LoadedSpeech(string Id, string Device);

    internal sealed class ProgressSink(IProgress<DownloadProgress>? progress)
    {
        [JSInvokable]
        public void Report(double fraction, string text) => progress?.Report(new DownloadProgress(fraction, text));
    }
}
