namespace Kamada.Modules.Assistant.Domain;

/// <summary>
/// What the language model understood from an utterance – plain mentions, never database ids.
/// Every value is trimmed and missing values are <c>null</c>; matching to real data happens later, in C#.
/// </summary>
internal sealed record ExtractedOrder(
    ExtractedClient Client,
    ExtractedAddress Address,
    IReadOnlyList<ExtractedLine> Lines,
    string? Notes)
{
    public bool IsEmpty => Client.IsEmpty && Address.IsEmpty && Lines.Count == 0;
}

/// <param name="Name">First name in the nominative form ("Jan", not "Jana").</param>
/// <param name="Phone">Digits only (optionally with a leading <c>+</c>).</param>
internal sealed record ExtractedClient(string? Name, string? Surname, string? Phone, string? Email)
{
    public static ExtractedClient None { get; } = new(null, null, null, null);

    public bool IsEmpty => Name is null && Surname is null && Phone is null && Email is null;
}

internal sealed record ExtractedAddress(string? Street, string? BuildingNumber, string? ApartmentNumber, string? City, string? PostalCode)
{
    public static ExtractedAddress None { get; } = new(null, null, null, null, null);

    public bool IsEmpty => Street is null && City is null;
}

/// <param name="Product">Product phrase in the base form, e.g. "woda niegazowana 1,5 l".</param>
/// <param name="Quantity">Positive number of units.</param>
internal sealed record ExtractedLine(string Product, int Quantity);
