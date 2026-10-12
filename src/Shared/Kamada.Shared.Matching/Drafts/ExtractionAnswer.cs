namespace Kamada.Shared.Matching.Drafts;

/// <summary>Reads the extraction model's answer, wherever the model runs (server or phone).</summary>
public static class ExtractionAnswer
{
    /// <summary>The cleaned extraction; <c>null</c> when the answer is not JSON of the expected shape.</summary>
    public static ExtractedOrder? Parse(string? text) => LlmOrderResponse.Parse(text)?.ToDomain();
}
