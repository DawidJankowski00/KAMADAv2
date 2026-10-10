using System.Net.Http.Json;

namespace Kamada.Web.Shared.Api;

/// <summary>
/// Thin typed wrapper over the API: JSON conventions, ProblemDetails parsing and transport errors in one place.
/// Authentication is added by <see cref="Auth.AuthorizingHandler"/>.
/// </summary>
public sealed class ApiClient(HttpClient http)
{
    public Task<ApiResult<T>> GetAsync<T>(string url, CancellationToken cancellationToken = default) =>
        SendAsync<T>(() => http.GetAsync(url, cancellationToken), cancellationToken);

    public Task<ApiResult<T>> PostAsync<T>(string url, object? body = null, CancellationToken cancellationToken = default) =>
        SendAsync<T>(() => http.PostAsJsonAsync(url, body, KamadaJson.Options, cancellationToken), cancellationToken);

    public Task<ApiResult> PostAsync(string url, object? body = null, CancellationToken cancellationToken = default) =>
        SendAsync(() => http.PostAsJsonAsync(url, body, KamadaJson.Options, cancellationToken), cancellationToken);

    public Task<ApiResult<T>> PutAsync<T>(string url, object body, CancellationToken cancellationToken = default) =>
        SendAsync<T>(() => http.PutAsJsonAsync(url, body, KamadaJson.Options, cancellationToken), cancellationToken);

    public Task<ApiResult> PutAsync(string url, object body, CancellationToken cancellationToken = default) =>
        SendAsync(() => http.PutAsJsonAsync(url, body, KamadaJson.Options, cancellationToken), cancellationToken);

    /// <summary>Uploads one file (plus optional text fields) as <c>multipart/form-data</c>.</summary>
    public Task<ApiResult<T>> PostFileAsync<T>(
        string url,
        string fieldName,
        byte[] content,
        string fileName,
        string contentType,
        IReadOnlyDictionary<string, string>? fields = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<T>(
            async () =>
            {
                using var form = new MultipartFormDataContent();
                var file = new ByteArrayContent(content);
                file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                form.Add(file, fieldName, fileName);
                foreach (var (name, value) in fields ?? new Dictionary<string, string>())
                {
                    form.Add(new StringContent(value), name);
                }

                return await http.PostAsync(url, form, cancellationToken);
            },
            cancellationToken);

    public Task<ApiResult<T>> DeleteAsync<T>(string url, CancellationToken cancellationToken = default) =>
        SendAsync<T>(() => http.DeleteAsync(url, cancellationToken), cancellationToken);

    public Task<ApiResult> DeleteAsync(string url, CancellationToken cancellationToken = default) =>
        SendAsync(() => http.DeleteAsync(url, cancellationToken), cancellationToken);

    private static async Task<ApiResult<T>> SendAsync<T>(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await send();
            if (!response.IsSuccessStatusCode)
            {
                return ApiResult<T>.Fail(await ReadProblemAsync(response, cancellationToken));
            }

            var value = await response.Content.ReadFromJsonAsync<T>(KamadaJson.Options, cancellationToken);
            return ApiResult<T>.Ok(value!);
        }
        catch (HttpRequestException)
        {
            return ApiResult<T>.Fail(ApiProblem.Network);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiResult<T>.Fail(ApiProblem.Timeout);
        }
    }

    private static async Task<ApiResult> SendAsync(Func<Task<HttpResponseMessage>> send, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await send();
            return response.IsSuccessStatusCode
                ? ApiResult.Ok()
                : ApiResult.Fail(await ReadProblemAsync(response, cancellationToken));
        }
        catch (HttpRequestException)
        {
            return ApiResult.Fail(ApiProblem.Network);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ApiResult.Fail(ApiProblem.Timeout);
        }
    }

    internal static async Task<ApiProblem> ReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken) =>
        ApiProblem.FromJson((int)response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
}
