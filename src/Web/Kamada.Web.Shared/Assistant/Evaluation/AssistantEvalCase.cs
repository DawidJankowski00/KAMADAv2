using System.Text.Json.Serialization;

namespace Kamada.Web.Shared.Assistant.Evaluation;

/// <summary>
/// One utterance with the expected outcome – the same <c>cases.json</c> the server evals use
/// (<c>benchmarks/Kamada.Assistant.Evals</c>).
/// <c>Client</c>: <c>Existing:Name Surname</c>, <c>New:Name Surname</c> or <c>Ambiguous</c>.
/// <c>Address</c>: <c>Existing:Street Building</c>, <c>New:Street Building</c> or <c>None</c>.
/// </summary>
public sealed record AssistantEvalCase(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("language")] string Language,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("client")] string Client,
    [property: JsonPropertyName("address")] string Address,
    [property: JsonPropertyName("lines")] IReadOnlyList<AssistantEvalLine> Lines);

/// <summary>
/// A product line: an exact <c>sku</c>; <c>ambiguous</c> SKUs the worker must choose from (nothing auto-selected);
/// or <c>notFound</c> (nothing auto-selected).
/// </summary>
public sealed record AssistantEvalLine(
    [property: JsonPropertyName("sku")] string? Sku,
    [property: JsonPropertyName("ambiguous")] IReadOnlyList<string>? Ambiguous,
    [property: JsonPropertyName("notFound")] bool NotFound,
    [property: JsonPropertyName("quantity")] int Quantity);

/// <summary>Outcome of one case: correctness per field plus what went wrong (for the report).</summary>
public sealed record AssistantEvalResult(
    AssistantEvalCase Case,
    long LatencyMs,
    bool ClientCorrect,
    bool AddressCorrect,
    int ProductsCorrect,
    int QuantitiesCorrect,
    int LinesCounted,
    IReadOnlyList<string> Problems)
{
    public bool AllCorrect => ClientCorrect && AddressCorrect && ProductsCorrect == LinesCounted && QuantitiesCorrect == LinesCounted;
}
