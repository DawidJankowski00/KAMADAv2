using Kamada.Shared.Contracts.Assistant;
using Kamada.Web.Shared.Api;
using Kamada.Web.Shared.Audio;

namespace Kamada.Web.Shared.Assistant;

/// <summary>The assistant on the server (<c>/api/assistant/...</c>).</summary>
public sealed class ServerOrderAssistant(ApiClient api) : IOrderAssistant
{
    public async Task<AssistantStatusDto?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var result = await api.GetAsync<AssistantStatusDto>("api/assistant/status", cancellationToken);
        return result.IsSuccess ? result.Value : null;
    }

    public Task<ApiResult<TranscriptionDto>> TranscribeAsync(RecordedAudio recording, CancellationToken cancellationToken = default) =>
        api.PostFileAsync<TranscriptionDto>(
            "api/assistant/transcriptions", "audio", recording.Wav, "nagranie.wav", "audio/wav",
            new Dictionary<string, string> { ["language"] = recording.Language },
            cancellationToken);

    public Task<ApiResult<OrderDraftDto>> CreateDraftAsync(string text, string language, CancellationToken cancellationToken = default) =>
        api.PostAsync<OrderDraftDto>("api/assistant/order-drafts", new OrderDraftRequest(text, language), cancellationToken);
}
