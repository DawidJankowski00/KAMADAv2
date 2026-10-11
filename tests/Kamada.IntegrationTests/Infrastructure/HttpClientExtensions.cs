using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Kamada.IntegrationTests.Infrastructure;

internal static class HttpClientExtensions
{
    /// <summary>Same JSON conventions as the API (web defaults + enums as strings).</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PostAsJsonAsync(url, body, Json, Ct);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string url, T body) =>
        client.PutAsJsonAsync(url, body, Json, Ct);

    public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json, Ct))!;

    public static async Task<string?> ReadProblemCodeAsync(this HttpResponseMessage response)
    {
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(Json, Ct);
        return problem?.Extensions.TryGetValue("code", out var code) == true ? code?.ToString() : null;
    }

    public static async Task<TokenResponse> LoginAsync(this HttpClient client, string username, string password)
    {
        var response = await client.PostJsonAsync("/api/auth/login", new LoginRequest(username, password));
        response.EnsureSuccessStatusCode();
        return await response.ReadAsAsync<TokenResponse>();
    }

    public static HttpClient WithBearer(this HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    /// <summary>Creates a fresh worker with the given role and returns a client signed in as that worker.</summary>
    public static async Task<(HttpClient Client, TestWorker Worker)> CreateSignedInClientAsync(
        this KamadaApiFactory factory,
        WorkerRole role)
    {
        var worker = await factory.CreateWorkerAsync(role);
        var client = factory.CreateClient();
        var tokens = await client.LoginAsync(worker.Username, worker.Password);
        return (client.WithBearer(tokens.AccessToken), worker);
    }

    public static async Task<HttpResponseMessage> GetIfNoneMatchAsync(this HttpClient client, string url, EntityTagHeaderValue etag)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.IfNoneMatch.Add(etag);
        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Whether a request with the tag just returned answers <c>304</c>. Other tests write to the same database in parallel, so
    /// the data may change between the two requests: a few attempts make that negligible.
    /// </summary>
    public static async Task<bool> AnswersNotModifiedForCurrentTagAsync(this HttpClient client, string url)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var etag = (await client.GetAsync(url, TestContext.Current.CancellationToken)).Headers.ETag!;
            var response = await client.GetIfNoneMatchAsync(url, etag);
            if (response.StatusCode == HttpStatusCode.NotModified)
            {
                (await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken)).ShouldBeEmpty();
                return true;
            }
        }

        return false;
    }
}
