using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Kamada.Shared.Infrastructure.Http;

public static class ETagHttpExtensions
{
    /// <summary>
    /// <c>200</c> with <paramref name="value"/> as JSON and an <c>ETag</c> (hash of the body), or <c>304 Not Modified</c>
    /// without a body when the request's <c>If-None-Match</c> carries that tag. The server still builds the data; what the
    /// tag saves is the download, which is what matters to a phone on a poor connection.
    /// </summary>
    public static IResult OkWithETag<T>(this HttpContext context, T value)
    {
        var jsonOptions = context.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
        var body = JsonSerializer.SerializeToUtf8Bytes(value, jsonOptions);
        var etag = new EntityTagHeaderValue($"\"{Convert.ToHexStringLower(SHA256.HashData(body).AsSpan(0, 16))}\"");

        var response = context.Response.GetTypedHeaders();
        response.ETag = etag;
        // Private data (clients): never kept by shared caches, and always revalidated.
        response.CacheControl = new CacheControlHeaderValue { Private = true, NoCache = true };

        var ifNoneMatch = context.Request.GetTypedHeaders().IfNoneMatch;
        if (ifNoneMatch.Any(tag => tag.Equals(EntityTagHeaderValue.Any) || tag.Compare(etag, useStrongComparison: false)))
        {
            return TypedResults.StatusCode(StatusCodes.Status304NotModified);
        }

        return TypedResults.Bytes(body, "application/json; charset=utf-8");
    }
}
