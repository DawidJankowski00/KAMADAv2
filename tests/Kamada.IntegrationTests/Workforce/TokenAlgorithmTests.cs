using System.Net;
using System.Security.Claims;
using System.Text;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Shared.Infrastructure.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WorkerRole = Kamada.Modules.Workforce.Domain.Workers.WorkerRole;

namespace Kamada.IntegrationTests.Workforce;

/// <summary>
/// Access tokens are accepted only with HS256, the algorithm the API signs with (ASVS 3.5.3): even with the right key,
/// a token whose header names another algorithm is refused.
/// </summary>
public sealed class TokenAlgorithmTests(KamadaApiFactory factory) : IAsyncDisposable
{
    // Long enough (512 bits) for HS512, so the other algorithms can be tried with the very same key.
    private const string SigningKey = "token-algorithm-tests-signing-key-0123456789-0123456789-01234567";

    private readonly WebApplicationFactory<Program> _host =
        factory.WithWebHostBuilder(builder => builder.UseSetting("Jwt:SigningKey", SigningKey));

    [Theory]
    [InlineData(SecurityAlgorithms.HmacSha256, HttpStatusCode.OK)]
    [InlineData(SecurityAlgorithms.HmacSha384, HttpStatusCode.Unauthorized)]
    [InlineData(SecurityAlgorithms.HmacSha512, HttpStatusCode.Unauthorized)]
    public async Task Me_AcceptsOnlyHs256(string algorithm, HttpStatusCode expected)
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Courier);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "kamada-api",
            Audience = "kamada-clients",
            Expires = DateTime.UtcNow.AddMinutes(5),
            Subject = new ClaimsIdentity([new Claim(KamadaClaimTypes.Subject, worker.Id.ToString())]),
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), algorithm),
        });

        var response = await _host.CreateClient().WithBearer(token).GetAsync("/api/auth/me", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(expected);
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
