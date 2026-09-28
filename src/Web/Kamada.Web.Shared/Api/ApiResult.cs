using System.Text.Json;

namespace Kamada.Web.Shared.Api;

/// <summary>
/// Error returned by the API (RFC 9457 ProblemDetails) or a transport failure (status 0).
/// </summary>
public sealed record ApiProblem(
    int Status,
    string Title,
    string? Detail = null,
    string? Code = null,
    IReadOnlyDictionary<string, string[]>? Errors = null)
{
    public static ApiProblem Network { get; } = new(0, "Brak połączenia z serwerem. Sprawdź połączenie z internetem.");

    /// <summary>Best message for the user: Polish text for known codes, otherwise the server's detail.</summary>
    public string Message =>
        (Code is not null ? ErrorMessages.TryTranslate(Code) : null)
        ?? FirstValidationError
        ?? Detail
        ?? Title;

    private string? FirstValidationError => Errors?.Values.SelectMany(e => e).FirstOrDefault();

    internal static ApiProblem FromJson(int status, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new ApiProblem(status, DefaultTitle(status));
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            Dictionary<string, string[]>? errors = null;
            if (root.TryGetProperty("errors", out var errorsElement) && errorsElement.ValueKind == JsonValueKind.Object)
            {
                errors = errorsElement.EnumerateObject().ToDictionary(
                    p => p.Name,
                    p => p.Value.ValueKind == JsonValueKind.Array
                        ? p.Value.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToArray()
                        : new[] { p.Value.ToString() });
            }

            return new ApiProblem(
                status,
                GetString(root, "title") ?? DefaultTitle(status),
                GetString(root, "detail"),
                GetString(root, "code"),
                errors);
        }
        catch (JsonException)
        {
            return new ApiProblem(status, DefaultTitle(status));
        }
    }

    private static string? GetString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static string DefaultTitle(int status) => status switch
    {
        400 => "Niepoprawne dane.",
        401 => "Sesja wygasła. Zaloguj się ponownie.",
        403 => "Brak uprawnień.",
        404 => "Nie znaleziono.",
        409 => "Operacja niedozwolona w obecnym stanie.",
        429 => "Zbyt wiele prób. Spróbuj za chwilę.",
        _ => "Wystąpił błąd serwera.",
    };
}

public class ApiResult
{
    protected ApiResult(ApiProblem? problem) => Problem = problem;

    public ApiProblem? Problem { get; }

    public bool IsSuccess => Problem is null;

    public static ApiResult Ok() => new(null);

    public static ApiResult Fail(ApiProblem problem) => new(problem);
}

public sealed class ApiResult<T> : ApiResult
{
    private readonly T? _value;

    private ApiResult(T? value, ApiProblem? problem)
        : base(problem) => _value = value;

    public T Value => IsSuccess ? _value! : throw new InvalidOperationException("The request failed; check Problem.");

    public static ApiResult<T> Ok(T value) => new(value, null);

    public static new ApiResult<T> Fail(ApiProblem problem) => new(default, problem);
}
