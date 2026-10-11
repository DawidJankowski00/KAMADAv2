namespace Kamada.Web.Shared.Connectivity;

public enum ConnectionQuality
{
    /// <summary>The server answers quickly: work online.</summary>
    Good,

    /// <summary>The server answers slowly or a ping failed: online requests may hang, prefer local data.</summary>
    Poor,

    /// <summary>No network (browser offline) or the server did not answer several pings in a row.</summary>
    Offline,
}
