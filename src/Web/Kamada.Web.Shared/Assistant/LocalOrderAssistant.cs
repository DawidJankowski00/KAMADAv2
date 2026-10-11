using Kamada.Shared.Contracts.Assistant;
using Kamada.Shared.Matching.Drafts;
using Kamada.Web.Shared.Api;
using Kamada.Web.Shared.Audio;
using Kamada.Web.Shared.Offline;
using Microsoft.JSInterop;

namespace Kamada.Web.Shared.Assistant;

/// <summary>
/// The assistant on the phone: Whisper turns the recording into text, the language model extracts mentions with the same
/// prompt and schema as the server, and the shared matchers (<see cref="OrderDraftResolver"/>) turn them into the same
/// <see cref="OrderDraftDto"/> against the phone's copy of clients and products. Nothing leaves the device.
/// </summary>
public sealed class LocalOrderAssistant(LocalAssistantRuntime runtime, LocalAssistantSettings settings, OfflineLookup lookup) : IOrderAssistant
{
    private readonly Dictionary<string, bool> _installed = [];

    /// <summary>Forget what is known about downloaded files (after a download or a delete on the settings page).</summary>
    public void Invalidate() => _installed.Clear();

    /// <summary>A language model is downloaded and the data the draft is matched against is on the phone.</summary>
    public async Task<bool> IsReadyAsync()
    {
        await settings.LoadAsync();
        if (!settings.Enabled || lookup.Products is null || lookup.Clients is null)
        {
            return false;
        }

        return (await runtime.GetCapabilitiesAsync()).CanRunLanguageModel && await IsInstalledAsync(settings.LanguageModel);
    }

    public async Task<AssistantStatusDto?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!await IsReadyAsync())
        {
            return null;
        }

        return new AssistantStatusDto(true, await IsInstalledAsync(settings.SpeechModel), settings.LanguageModel.Title);
    }

    public async Task<ApiResult<TranscriptionDto>> TranscribeAsync(RecordedAudio recording, CancellationToken cancellationToken = default)
    {
        try
        {
            await runtime.LoadAsync(settings.SpeechModel);
            var text = await runtime.TranscribeAsync(recording.Wav, recording.Language);
            return string.IsNullOrWhiteSpace(text)
                ? Fail("Assistant.NothingHeard")
                : ApiResult<TranscriptionDto>.Ok(new TranscriptionDto(text, recording.Language, (long)recording.Duration.TotalMilliseconds));
        }
        catch (JSException)
        {
            return Fail("Assistant.DeviceModelFailed");
        }
    }

    public async Task<ApiResult<OrderDraftDto>> CreateDraftAsync(string text, string language, CancellationToken cancellationToken = default)
    {
        if (!await IsReadyAsync())
        {
            return ApiResult<OrderDraftDto>.Fail(Problem("Assistant.DeviceNotReady"));
        }

        string answer;
        try
        {
            await runtime.LoadAsync(settings.LanguageModel);
            answer = await runtime.ExtractAsync(text.Trim(), AssistantLanguages.Parse(language));
        }
        catch (JSException)
        {
            return ApiResult<OrderDraftDto>.Fail(Problem("Assistant.DeviceModelFailed"));
        }

        var extracted = ExtractionAnswer.Parse(answer);
        if (extracted is null)
        {
            return ApiResult<OrderDraftDto>.Fail(Problem("Assistant.ExtractionFailed"));
        }

        if (extracted.IsEmpty)
        {
            return ApiResult<OrderDraftDto>.Fail(Problem("Assistant.NothingRecognized"));
        }

        var draft = await new OrderDraftResolver(new LocalDraftSource(lookup.Clients?.Items ?? [], lookup.Products?.Items ?? [])).ResolveAsync(extracted, text.Trim(), cancellationToken);
        return ApiResult<OrderDraftDto>.Ok(draft);
    }

    private async Task<bool> IsInstalledAsync(LocalModel model)
    {
        if (!_installed.TryGetValue(model.Id, out var installed))
        {
            installed = await runtime.IsInstalledAsync(model);
            _installed[model.Id] = installed;
        }

        return installed;
    }

    private static ApiProblem Problem(string code) => new(400, code, null, code);

    private static ApiResult<TranscriptionDto> Fail(string code) => ApiResult<TranscriptionDto>.Fail(Problem(code));
}
