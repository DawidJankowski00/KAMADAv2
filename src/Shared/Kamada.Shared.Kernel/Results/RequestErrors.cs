namespace Kamada.Shared.Kernel.Results;

/// <summary>Errors of idempotent creates (requests carrying a client-generated <c>RequestId</c>).</summary>
public static class RequestErrors
{
    /// <summary>The id was already used by a request that created something else (another client's address, another worker's order).</summary>
    public static readonly Error IdReused =
        Error.Conflict("Requests.IdReused", "This request id was already used for a different request.");
}
