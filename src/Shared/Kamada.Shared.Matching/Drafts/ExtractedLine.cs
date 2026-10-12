namespace Kamada.Shared.Matching.Drafts;

/// <param name="Product">Product phrase in the base form, e.g. "woda niegazowana 1,5 l".</param>
/// <param name="Quantity">Positive number of units.</param>
public sealed record ExtractedLine(string Product, int Quantity);
