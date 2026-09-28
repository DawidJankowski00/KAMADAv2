using Microsoft.AspNetCore.Http;

namespace Kamada.Shared.Infrastructure.Auth;

/// <summary>
/// The authenticated worker making the current request.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Worker id from the <c>sub</c> claim. Throws when the request is anonymous.</summary>
    Guid Id { get; }

    bool IsInRole(string role);
}

internal sealed class HttpContextCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public bool IsAuthenticated => accessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid Id =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirst(KamadaClaimTypes.Subject)?.Value, out var id)
            ? id
            : throw new InvalidOperationException("The current request is not authenticated.");

    public bool IsInRole(string role) => accessor.HttpContext?.User.IsInRole(role) == true;
}
