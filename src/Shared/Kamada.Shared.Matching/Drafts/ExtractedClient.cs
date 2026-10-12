namespace Kamada.Shared.Matching.Drafts;

/// <param name="Name">First name in the nominative form ("Jan", not "Jana").</param>
/// <param name="Phone">Digits only (optionally with a leading <c>+</c>).</param>
public sealed record ExtractedClient(string? Name, string? Surname, string? Phone, string? Email)
{
    public static ExtractedClient None { get; } = new(null, null, null, null);

    public bool IsEmpty => Name is null && Surname is null && Phone is null && Email is null;
}
