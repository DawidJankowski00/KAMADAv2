using System.Text.Json;
using Kamada.Web.Shared.Api;
using Microsoft.JSInterop;

namespace Kamada.Web.Shared.Assistant;

/// <summary>
/// The worker's choices for the on-device assistant: which models, and which assistant to use. Kept in <c>localStorage</c>
/// (not in the local store, which sign-out wipes: these are settings of the device, not data of the worker).
/// </summary>
public sealed class LocalAssistantSettings(IJSRuntime js, KamadaClientOptions options)
{
    private Preferences? _loaded;

    public event Action? Changed;

    public AssistantMode Mode { get; private set; } = AssistantMode.Auto;

    public LocalModel LanguageModel { get; private set; } = LocalModels.DefaultLanguage;

    public LocalModel SpeechModel { get; private set; } = LocalModels.DefaultSpeech;

    /// <summary>The worker downloaded the models and wants the phone to use them.</summary>
    public bool Enabled { get; private set; }

    private string Key => options.StorageKey + ".assistant";

    public async Task LoadAsync()
    {
        if (_loaded is not null)
        {
            return;
        }

        Preferences stored = new();
        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", Key);
            if (json is not null)
            {
                stored = JsonSerializer.Deserialize<Preferences>(json, KamadaJson.Options) ?? stored;
            }
        }
        catch (Exception ex) when (ex is JSException or JsonException)
        {
            // Storage blocked or damaged: defaults.
        }

        Apply(stored);
        _loaded = stored;
    }

    public Task SetModeAsync(AssistantMode mode) => UpdateAsync(p => p with { Mode = mode });

    public Task SetLanguageModelAsync(LocalModel model) => UpdateAsync(p => p with { LanguageModelId = model.Id });

    public Task SetSpeechModelAsync(LocalModel model) => UpdateAsync(p => p with { SpeechModelId = model.Id });

    public Task SetEnabledAsync(bool enabled) => UpdateAsync(p => p with { Enabled = enabled });

    private async Task UpdateAsync(Func<Preferences, Preferences> change)
    {
        await LoadAsync();
        var updated = change(_loaded!);
        Apply(updated);
        _loaded = updated;
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", Key, JsonSerializer.Serialize(updated, KamadaJson.Options));
        }
        catch (JSException)
        {
            // The choice then lasts until the page is closed.
        }

        Changed?.Invoke();
    }

    private void Apply(Preferences preferences)
    {
        Mode = preferences.Mode;
        Enabled = preferences.Enabled;
        LanguageModel = LocalModels.Find(preferences.LanguageModelId) is { Kind: LocalModelKind.Language } language ? language : LocalModels.DefaultLanguage;
        SpeechModel = LocalModels.Find(preferences.SpeechModelId) is { Kind: LocalModelKind.Speech } speech ? speech : LocalModels.DefaultSpeech;
    }

    private sealed record Preferences(AssistantMode Mode = AssistantMode.Auto, string? LanguageModelId = null, string? SpeechModelId = null, bool Enabled = false);
}
