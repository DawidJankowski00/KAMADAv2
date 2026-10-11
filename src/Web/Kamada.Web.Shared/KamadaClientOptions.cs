namespace Kamada.Web.Shared;

public sealed class KamadaClientOptions
{
    /// <summary>API root, e.g. <c>http://localhost:5080/</c> (read from <c>wwwroot/appsettings.json</c>).</summary>
    public required Uri ApiBaseUrl { get; init; }

    /// <summary>Users with at least one of these roles may sign in to the app (role names from <c>Roles</c>).</summary>
    public required IReadOnlyCollection<string> AllowedRoles { get; init; }

    /// <summary>localStorage key for the session, different per app.</summary>
    public required string StorageKey { get; init; }

    /// <summary>Key the app used before a rename; a session found there is moved to <see cref="StorageKey"/> once.</summary>
    public string? PreviousStorageKey { get; init; }

    /// <summary>Where snackbars appear (<c>MudBlazor.Defaults.Classes.Position</c>); on phones the bottom is taken by action buttons.</summary>
    public string SnackbarPosition { get; init; } = MudBlazor.Defaults.Classes.Position.BottomRight;
}
