using Kamada.Modules.Workforce.Domain.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace Kamada.Modules.Workforce.Api;

internal static class SessionClientExtensions
{
    /// <summary>
    /// Device of the request. The IP is the connection's remote address; behind a reverse proxy it is right only once
    /// forwarded headers are configured for that proxy.
    /// </summary>
    public static SessionClient ToSessionClient(this HttpContext context) =>
        new(context.Request.Headers[HeaderNames.UserAgent].ToString(), context.Connection.RemoteIpAddress?.ToString());
}
