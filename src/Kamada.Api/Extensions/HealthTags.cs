namespace Kamada.Api.Extensions;

internal static class HealthTags
{
    /// <summary>Checks included in <c>/health/ping</c> (cheap enough to be polled by every phone).</summary>
    public const string Ping = "ping";
}
