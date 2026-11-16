using Microsoft.Net.Http.Headers;

namespace Kamada.Api.Extensions;

/// <summary>
/// Response headers that keep browsers from misusing API responses: no MIME sniffing, no framing, no referrer leaks,
/// and for JSON under <c>/api</c> a policy that forbids loading or running anything (the API never serves a page) and
/// <c>Cache-Control: no-store</c> unless the endpoint set its own caching.
/// HSTS is added outside Development by <c>UseHsts</c> (only on HTTPS requests, i.e. behind the TLS proxy).
/// </summary>
internal static class SecurityHeaders
{
    private const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    // The API reference page (/scalar, not in Production) loads its own scripts and styles: only framing is forbidden.
    private const string PageContentSecurityPolicy = "frame-ancestors 'none'";

    public static IApplicationBuilder UseKamadaSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers[HeaderNames.XContentTypeOptions] = "nosniff";
                headers[HeaderNames.XFrameOptions] = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                headers[HeaderNames.ContentSecurityPolicy] = context.Request.Path.StartsWithSegments("/api")
                    ? ApiContentSecurityPolicy
                    : PageContentSecurityPolicy;

                // API answers hold personal data: no browser or proxy keeps them, unless an endpoint chose its own policy
                // (the ETag lookups of the phone app use private, no-cache to revalidate).
                if (context.Request.Path.StartsWithSegments("/api") && !headers.ContainsKey(HeaderNames.CacheControl))
                {
                    headers[HeaderNames.CacheControl] = "no-store";
                }

                return Task.CompletedTask;
            });

            await next(context);
        });
}
