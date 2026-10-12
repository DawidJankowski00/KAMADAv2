namespace Kamada.Shared.Matching.Drafts;

/// <summary>
/// What the language model understood from an utterance – plain mentions, never database ids.
/// Every value is trimmed and missing values are <c>null</c>; matching to real data happens later, in C#.
/// </summary>
public sealed record ExtractedOrder(
    ExtractedClient Client,
    ExtractedAddress Address,
    IReadOnlyList<ExtractedLine> Lines,
    string? Notes)
{
    public bool IsEmpty => Client.IsEmpty && Address.IsEmpty && Lines.Count == 0;
}
