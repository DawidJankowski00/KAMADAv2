using Kamada.Shared.Contracts.Assistant;
using Kamada.Web.Shared.Api;
using Kamada.Web.Shared.Audio;

namespace Kamada.Web.Shared.Assistant;

/// <summary>
/// Turns dictated or typed notes into order drafts. The form talks only to this interface; both apps use the server's
/// assistant (<see cref="ServerOrderAssistant"/>).
/// </summary>
public interface IOrderAssistant
{
    /// <summary>What this assistant can do right now; <c>null</c> when it cannot be reached.</summary>
    Task<AssistantStatusDto?> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<ApiResult<TranscriptionDto>> TranscribeAsync(RecordedAudio recording, CancellationToken cancellationToken = default);

    /// <summary>Matches the note against clients and products; nothing is saved.</summary>
    Task<ApiResult<OrderDraftDto>> CreateDraftAsync(string text, string language, CancellationToken cancellationToken = default);
}
