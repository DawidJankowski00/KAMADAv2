namespace Kamada.Modules.Delivery.Domain.Runs;

/// <summary>
/// Stored Google Maps link for a part of the run. Point indexes: 0 = start, 1..n = stops in sequence, n + 1 = end.
/// </summary>
internal sealed record RunNavigationLink(int Part, int TotalParts, int FromPoint, int ToPoint, string Url);
