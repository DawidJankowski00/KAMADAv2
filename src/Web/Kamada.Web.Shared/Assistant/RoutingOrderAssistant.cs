using Kamada.Shared.Contracts.Assistant;
using Kamada.Web.Shared.Api;
using Kamada.Web.Shared.Audio;
using Kamada.Web.Shared.Connectivity;

namespace Kamada.Web.Shared.Assistant;

/// <summary>
/// The phone app's <see cref="IOrderAssistant"/>: the server's assistant while the connection is good, the model on the phone
/// when it is poor or lost, when the server cannot help (switched off, no answer), or when the worker chose so
/// (<see cref="AssistantPolicy"/>). <see cref="Location"/> tells the panel which one answers, for the chip over it.
/// </summary>
public sealed class RoutingOrderAssistant(
    ServerOrderAssistant server,
    LocalOrderAssistant local,
    LocalAssistantSettings settings,
    ConnectionMonitor monitor) : IOrderAssistant
{
    private bool? _serverEnabled;
    private bool _deviceReady;

    /// <summary>Where the next request goes (as far as it is known without asking again).</summary>
    public AssistantLocation Location { get; private set; } = AssistantLocation.Server;

    /// <summary>Whether the model on the phone could answer (so the worker can be offered the choice).</summary>
    public bool DeviceReady => _deviceReady;

    public event Action? LocationChanged;

    public async Task<AssistantStatusDto?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await settings.LoadAsync();
        _deviceReady = await local.IsReadyAsync();

        AssistantStatusDto? serverStatus = null;
        var asked = false;
        if (settings.Mode != AssistantMode.Device && monitor.Quality == ConnectionQuality.Good)
        {
            (serverStatus, asked) = (await server.GetStatusAsync(cancellationToken), true);
            _serverEnabled = serverStatus?.Enabled;
        }

        var chosen = ChooseLocation();
        SetLocation(chosen ?? AssistantLocation.Server);
        return chosen switch
        {
            AssistantLocation.Server => asked ? serverStatus : await server.GetStatusAsync(cancellationToken),
            AssistantLocation.Device => await local.GetStatusAsync(cancellationToken),
            _ => null,
        };
    }

    public async Task<ApiResult<TranscriptionDto>> TranscribeAsync(RecordedAudio recording, CancellationToken cancellationToken = default)
    {
        if (await UseDeviceAsync())
        {
            return await local.TranscribeAsync(recording, cancellationToken);
        }

        var result = await server.TranscribeAsync(recording, cancellationToken);
        return !result.IsSuccess && await CanFallBackAsync(result) ? await local.TranscribeAsync(recording, cancellationToken) : result;
    }

    public async Task<ApiResult<OrderDraftDto>> CreateDraftAsync(string text, string language, CancellationToken cancellationToken = default)
    {
        if (await UseDeviceAsync())
        {
            return await local.CreateDraftAsync(text, language, cancellationToken);
        }

        var result = await server.CreateDraftAsync(text, language, cancellationToken);
        return !result.IsSuccess && await CanFallBackAsync(result) ? await local.CreateDraftAsync(text, language, cancellationToken) : result;
    }

    private AssistantLocation? ChooseLocation() => AssistantPolicy.Choose(settings.Mode, monitor.Quality, _serverEnabled, _deviceReady);

    private async Task<bool> UseDeviceAsync()
    {
        await settings.LoadAsync();
        _deviceReady = await local.IsReadyAsync();
        var chosen = ChooseLocation();
        SetLocation(chosen ?? AssistantLocation.Server);
        return chosen == AssistantLocation.Device;
    }

    /// <summary>The server failed in a way the phone can make up for – only in automatic mode, only when the phone is ready.</summary>
    private async Task<bool> CanFallBackAsync(ApiResult result)
    {
        if (settings.Mode != AssistantMode.Auto || result.Problem is not { } problem || !AssistantPolicy.ShouldFallBack(problem)
            || !await local.IsReadyAsync())
        {
            return false;
        }

        SetLocation(AssistantLocation.Device);
        return true;
    }

    private void SetLocation(AssistantLocation location)
    {
        if (Location != location)
        {
            Location = location;
            LocationChanged?.Invoke();
        }
    }
}
