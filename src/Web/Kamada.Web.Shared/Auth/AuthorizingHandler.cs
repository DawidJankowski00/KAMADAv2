using System.Net;
using System.Net.Http.Headers;

namespace Kamada.Web.Shared.Auth;

/// <summary>
/// Adds the bearer token to API calls; on <c>401</c> refreshes the token once and retries the request.
/// </summary>
public sealed class AuthorizingHandler(AuthService auth) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = await auth.GetAccessTokenAsync(cancellationToken);
        if (token is null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // Buffer the body so the request can be replayed after a refresh.
        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync(cancellationToken);
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        var refreshed = await auth.RefreshAsync(token, cancellationToken);
        if (refreshed is null)
        {
            return response;
        }

        response.Dispose();
        using var retry = await CloneAsync(request, cancellationToken);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed);
        return await base.SendAsync(retry, cancellationToken);
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri) { Version = request.Version };
        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (request.Content is not null)
        {
            var body = await request.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(body);
            foreach (var header in request.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}
