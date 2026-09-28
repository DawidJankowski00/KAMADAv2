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
}
