using System.Text.Json.Serialization;

namespace Kamada.Assistant.Evals;

/// <summary>
/// One utterance with the expected outcome.
/// <c>Client</c>: <c>Existing:Name Surname</c>, <c>New:Name Surname</c> or <c>Ambiguous</c>.
/// <c>Address</c>: <c>Existing:Street Building</c>, <c>New:Street Building</c> or <c>None</c>.
/// </summary>
internal sealed record EvalCase(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("language")] string Language,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("client")] string Client,
    [property: JsonPropertyName("address")] string Address,
    [property: JsonPropertyName("lines")] IReadOnlyList<ExpectedLine> Lines);

/// <summary>
/// A product line: an exact <c>sku</c>; <c>ambiguous</c> SKUs the admin must choose from (nothing auto-selected);
/// or <c>notFound</c> (nothing auto-selected).
/// </summary>
internal sealed record ExpectedLine(
    [property: JsonPropertyName("sku")] string? Sku,
    [property: JsonPropertyName("ambiguous")] IReadOnlyList<string>? Ambiguous,
    [property: JsonPropertyName("notFound")] bool NotFound,
    [property: JsonPropertyName("quantity")] int Quantity);
