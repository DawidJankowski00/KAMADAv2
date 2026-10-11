using System.Net;
using System.Net.Http.Json;
using Kamada.Shared.Contracts.Auth;
using Kamada.Web.Shared.Api;

namespace Kamada.Web.Shared.UnitTests.Auth;

/// <summary>Answers the auth endpoints with prepared token responses and records the requested paths.</summary>
internal sealed class StubApi : HttpMessageHandler, IHttpClientFactory
{
    public TokenResponse? LoginResponse { get; set; }

    public TokenResponse? RefreshResponse { get; set; }

    public List<string> RequestedPaths { get; } = [];

    public HttpClient CreateClient(string name) => new(this, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };

    public static TokenResponse Tokens(params string[] roles) => new(
        "access-" + Guid.NewGuid(),
        DateTimeOffset.UtcNow.AddMinutes(15),
        "refresh-" + Guid.NewGuid(),
        DateTimeOffset.UtcNow.AddDays(7),
        new AuthenticatedUserDto(Guid.NewGuid(), "anna", "Anna", "Nowak", roles));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        RequestedPaths.Add(path);
        var body = path switch
        {
            "/api/auth/login" => LoginResponse,
            "/api/auth/refresh" => RefreshResponse,
            _ => null,
        };

        return Task.FromResult(body is null
            ? new HttpResponseMessage(path == "/api/auth/logout" ? HttpStatusCode.NoContent : HttpStatusCode.Unauthorized)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body, options: KamadaJson.Options) });
    }
}
