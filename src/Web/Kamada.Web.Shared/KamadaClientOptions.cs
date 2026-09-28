namespace Kamada.Web.Shared;

public sealed class KamadaClientOptions
{
    /// <summary>API root, e.g. <c>http://localhost:5080/</c> (read from <c>wwwroot/appsettings.json</c>).</summary>
    public required Uri ApiBaseUrl { get; init; }

    /// <summary>Only users with this role may sign in to the app (Admin panel / Courier app).</summary>
    public required string RequiredRole { get; init; }

    /// <summary>localStorage key for the session, different per app.</summary>
    public required string StorageKey { get; init; }
}
