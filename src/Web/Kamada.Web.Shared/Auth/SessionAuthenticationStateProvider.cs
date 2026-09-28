using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Kamada.Web.Shared.Auth;

/// <summary>
/// Exposes the stored session to Blazor's <c>[Authorize]</c> / <c>AuthorizeView</c>. The server remains the
/// authority: client-side checks only decide what to render.
/// </summary>
public sealed class SessionAuthenticationStateProvider : AuthenticationStateProvider, IDisposable
{
    private static readonly AuthenticationState Anonymous = new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly AuthService _auth;

    public SessionAuthenticationStateProvider(AuthService auth)
    {
        _auth = auth;
        _auth.SessionChanged += OnSessionChanged;
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var user = await _auth.GetCurrentUserAsync();
        if (user is null)
        {
            return Anonymous;
        }

        var identity = new ClaimsIdentity(
            [
                new Claim("sub", user.Id.ToString()),
                new Claim("name", user.Username),
                new Claim("given_name", user.Name),
                new Claim("family_name", user.Surname),
                new Claim("role", user.Role),
            ],
            authenticationType: "kamada",
            nameType: "name",
            roleType: "role");
        return new AuthenticationState(new ClaimsPrincipal(identity));
    }

    public void Dispose() => _auth.SessionChanged -= OnSessionChanged;

    private void OnSessionChanged() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
}
