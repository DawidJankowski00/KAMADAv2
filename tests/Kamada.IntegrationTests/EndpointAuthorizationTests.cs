using Kamada.IntegrationTests.Infrastructure;
using Kamada.Shared.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.IntegrationTests;

/// <summary>
/// Every endpoint states who may call it: a named policy, or <c>AllowAnonymous</c> – and anonymous ones are exactly the
/// expected few. A new endpoint without a decision fails here instead of quietly relying on the fallback policy.
/// </summary>
public sealed class EndpointAuthorizationTests(KamadaApiFactory factory)
{
    private static readonly string[] KnownPolicies =
    [
        AuthorizationPolicies.SignedIn,
        AuthorizationPolicies.AdminOnly,
        AuthorizationPolicies.CourierOnly,
        AuthorizationPolicies.SalesAccess,
        AuthorizationPolicies.DeveloperOnly,
    ];

    private static readonly string[] AnonymousEndpoints =
    [
        "POST /api/auth/login",
        "POST /api/auth/login/two-factor",
        "POST /api/auth/refresh",
        "POST /api/auth/forgot-password",
        "POST /api/auth/reset-password",
        "ANY /health",
        "ANY /health/ping",
        "GET /openapi/{documentName}.json",
    ];

    [Fact]
    public void EveryEndpoint_HasANamedPolicyOrIsDeliberatelyAnonymous()
    {
        var endpoints = Endpoints();
        var undecided = endpoints
            .Where(e => !IsAnonymous(e) && !Policies(e).Any())
            .Select(Describe)
            .ToList();

        undecided.ShouldBeEmpty();
    }

    [Fact]
    public void EveryPolicyUsed_IsAKnownOne()
    {
        var unknown = Endpoints()
            .SelectMany(e => Policies(e).Where(p => !KnownPolicies.Contains(p)).Select(p => $"{Describe(e)} uses {p}"))
            .ToList();

        unknown.ShouldBeEmpty();
    }

    [Fact]
    public void AnonymousEndpoints_AreExactlyTheExpectedOnes()
    {
        // The API reference (Scalar: page and its own assets, not in Production) is anonymous by design.
        var anonymous = Endpoints()
            .Where(e => IsAnonymous(e) && e.RoutePattern.RawText?.StartsWith("/scalar", StringComparison.Ordinal) != true)
            .Select(Describe)
            .Order()
            .ToList();

        anonymous.ShouldBe(AnonymousEndpoints.Order().ToList());
    }

    private List<RouteEndpoint> Endpoints() =>
        [.. factory.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()];

    private static bool IsAnonymous(RouteEndpoint endpoint) => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;

    private static IEnumerable<string> Policies(RouteEndpoint endpoint) =>
        endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy).OfType<string>();

    private static string Describe(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods ?? ["ANY"];
        return $"{string.Join(",", methods)} /{endpoint.RoutePattern.RawText?.TrimStart('/')}";
    }
}
