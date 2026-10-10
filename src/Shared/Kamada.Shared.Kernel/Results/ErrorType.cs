namespace Kamada.Shared.Kernel.Results;

/// <summary>
/// Category of a business failure. Drives the HTTP status code the API returns.
/// </summary>
public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,

    /// <summary>A dependency (e.g. a local AI model) is temporarily unavailable; the request may be retried.</summary>
    Unavailable,
}
