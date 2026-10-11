using Microsoft.JSInterop;

namespace Kamada.Web.Shared.Auth;

/// <summary>
/// The role a worker with several roles is currently working in. Purely a client-side choice of views: the token
/// carries every role and the API checks roles per endpoint.
/// </summary>
/// <remarks>
/// Available roles = the user's roles the app supports (<see cref="KamadaClientOptions.AllowedRoles"/>, in that order).
/// The active role is remembered per user in <c>localStorage</c>. Default: the only role, otherwise the last used one,
/// otherwise the first available in the app's order.
/// </remarks>
public sealed class RoleContext : IDisposable
{
    private readonly AuthService _auth;
    private readonly IJSRuntime _js;
    private readonly KamadaClientOptions _options;

    private Guid? _userId;

    public RoleContext(AuthService auth, IJSRuntime js, KamadaClientOptions options)
    {
        _auth = auth;
        _js = js;
        _options = options;
        _auth.SessionChanged += OnSessionChanged;
    }

    /// <summary>Raised when the active role or the available roles change (switch, login, logout, roles changed by an admin).</summary>
    public event Action? ActiveRoleChanged;

    public IReadOnlyList<string> AvailableRoles { get; private set; } = [];

    /// <summary>Null when signed out.</summary>
    public string? ActiveRole { get; private set; }

    /// <summary>Resolves the active role for the signed-in user; cheap when nothing changed since the last call.</summary>
    public async Task<string?> LoadAsync()
    {
        var user = await _auth.GetCurrentUserAsync();
        var available = user is null ? [] : _options.AllowedRoles.Where(user.Roles.Contains).ToList();
        if (user?.Id == _userId && available.SequenceEqual(AvailableRoles))
        {
            return ActiveRole;
        }

        var active = available.Count switch
        {
            0 => null,
            1 => available[0],
            _ => await ReadRememberedRoleAsync(user!.Id) is { } remembered && available.Contains(remembered) ? remembered : available[0],
        };

        // Assigned together after the last await: a concurrent call (e.g. the login page and SessionChanged) must never
        // see the new user with the previous active role.
        (_userId, AvailableRoles, ActiveRole) = (user?.Id, available, active);
        return ActiveRole;
    }

    /// <summary>Makes <paramref name="role"/> the active role; false when the user does not have it in this app.</summary>
    public async Task<bool> SwitchToAsync(string role)
    {
        await LoadAsync();
        if (_userId is not { } userId || !AvailableRoles.Contains(role))
        {
            return false;
        }

        if (role != ActiveRole)
        {
            ActiveRole = role;
            await RememberRoleAsync(userId, role);
            ActiveRoleChanged?.Invoke();
        }

        return true;
    }

    public void Dispose() => _auth.SessionChanged -= OnSessionChanged;

    private async void OnSessionChanged()
    {
        var before = (ActiveRole, AvailableRoles);
        await LoadAsync();
        if (before.ActiveRole != ActiveRole || !before.AvailableRoles.SequenceEqual(AvailableRoles))
        {
            ActiveRoleChanged?.Invoke();
        }
    }

    private string StorageKey(Guid userId) => $"{_options.StorageKey}.role.{userId:N}";

    private async ValueTask<string?> ReadRememberedRoleAsync(Guid userId)
    {
        try
        {
            return await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey(userId));
        }
        catch (JSException)
        {
            return null;
        }
    }

    private async ValueTask RememberRoleAsync(Guid userId, string role)
    {
        try
        {
            await _js.InvokeVoidAsync("localStorage.setItem", StorageKey(userId), role);
        }
        catch (JSException)
        {
            // The choice lasts until the page is reloaded.
        }
    }
}
