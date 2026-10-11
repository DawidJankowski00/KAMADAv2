using System.Net.Http.Headers;
using System.Net.Http.Json;
using Kamada.Shared.Contracts.Auth;
using Kamada.Web.Shared.Api;

namespace Kamada.Web.Shared.Auth;

/// <summary>
/// Owns the session: login, logout and transparent access-token refresh (with refresh-token rotation).
/// Registered as a singleton so the HTTP handler and the UI see the same session.
/// </summary>
public sealed class AuthService(IHttpClientFactory httpClientFactory, TokenStore store, KamadaClientOptions options, TimeProvider clock)
    : IDisposable
{
    public const string AnonymousClientName = "kamada-auth";

    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(30);

    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    /// <summary>Raised after login, logout or when the session expired.</summary>
    public event Action? SessionChanged;

    public async ValueTask<AuthenticatedUserDto?> GetCurrentUserAsync()
    {
        var session = await store.GetAsync();
        return session is not null && session.RefreshTokenExpiresAt > clock.GetUtcNow() ? session.User : null;
    }

    public async Task<ApiResult<AuthenticatedUserDto>> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var http = httpClientFactory.CreateClient(AnonymousClientName);
        TokenResponse tokens;
        try
        {
            using var response = await http.PostAsJsonAsync("api/auth/login", new LoginRequest(username, password), KamadaJson.Options, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return ApiResult<AuthenticatedUserDto>.Fail(await ApiClient.ReadProblemAsync(response, cancellationToken));
            }

            tokens = (await response.Content.ReadFromJsonAsync<TokenResponse>(KamadaJson.Options, cancellationToken))!;
        }
        catch (HttpRequestException)
        {
            return ApiResult<AuthenticatedUserDto>.Fail(ApiProblem.Network);
        }

        if (!tokens.User.Roles.Contains(options.RequiredRole))
        {
            await RevokeAsync(tokens, cancellationToken);
            return ApiResult<AuthenticatedUserDto>.Fail(new ApiProblem(403, "To konto nie ma dostępu do tej aplikacji."));
        }

        await store.SetAsync(tokens);
        SessionChanged?.Invoke();
        return ApiResult<AuthenticatedUserDto>.Ok(tokens.User);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var session = await store.GetAsync();
        if (session is not null)
        {
            await RevokeAsync(session, cancellationToken);
        }

        await store.ClearAsync();
        SessionChanged?.Invoke();
    }

    /// <summary>Access token valid for at least a few more seconds, refreshed if needed; null when signed out.</summary>
    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var session = await store.GetAsync();
        if (session is null)
        {
            return null;
        }

        return session.AccessTokenExpiresAt - RefreshMargin > clock.GetUtcNow()
            ? session.AccessToken
            : await RefreshAsync(session.AccessToken, cancellationToken);
    }

    /// <summary>
    /// Exchanges the refresh token for a new pair. Concurrent callers wait for one refresh; a caller whose token
    /// was already replaced by another refresh just gets the new token (rotated tokens must not be reused).
    /// </summary>
    public async Task<string?> RefreshAsync(string staleAccessToken, CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var session = await store.GetAsync();
            if (session is null)
            {
                return null;
            }

            if (session.AccessToken != staleAccessToken && session.AccessTokenExpiresAt - RefreshMargin > clock.GetUtcNow())
            {
                return session.AccessToken;
            }

            var http = httpClientFactory.CreateClient(AnonymousClientName);
            try
            {
                using var response = await http.PostAsJsonAsync("api/auth/refresh", new RefreshTokenRequest(session.RefreshToken), KamadaJson.Options, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var tokens = (await response.Content.ReadFromJsonAsync<TokenResponse>(KamadaJson.Options, cancellationToken))!;
                    await store.SetAsync(tokens);
                    return tokens.AccessToken;
                }
            }
            catch (HttpRequestException)
            {
                // Offline: keep the session, the request will fail with a network error instead of a logout.
                return session.AccessToken;
            }

            await store.ClearAsync();
            SessionChanged?.Invoke();
            return null;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Dispose() => _refreshLock.Dispose();

    private async Task RevokeAsync(TokenResponse session, CancellationToken cancellationToken)
    {
        try
        {
            var http = httpClientFactory.CreateClient(AnonymousClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/logout")
            {
                Content = JsonContent.Create(new LogoutRequest(session.RefreshToken), options: KamadaJson.Options),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
            using var _ = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // Best effort: the refresh token expires on its own.
        }
    }
}
