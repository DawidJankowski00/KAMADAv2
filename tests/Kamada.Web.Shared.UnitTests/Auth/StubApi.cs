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

    /// <summary>Answer to change-password; null means 400 with the code <c>Auth.CurrentPasswordInvalid</c>.</summary>
    public TokenResponse? ChangePasswordResponse { get; set; }

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
        if (path == "/api/auth/forgot-password")
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Accepted));
        }

        if (path == "/api/auth/change-password" && ChangePasswordResponse is null)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"status":400,"title":"Bad Request","code":"Auth.CurrentPasswordInvalid"}""", System.Text.Encoding.UTF8, "application/problem+json"),
            });
        }

        object? body = path switch
        {
            "/api/auth/login" => LoginResponse is null ? null : new LoginResponse(LoginResponse, null),
            "/api/auth/refresh" => RefreshResponse,
            "/api/auth/change-password" => ChangePasswordResponse,
            _ => null,
        };

        return Task.FromResult(body is null
            ? new HttpResponseMessage(path == "/api/auth/logout" ? HttpStatusCode.NoContent : HttpStatusCode.Unauthorized)
            : new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body, options: KamadaJson.Options) });
    }
}
