namespace Kamada.Modules.RouteOptimization.Contracts;

/// <summary>
/// One Google Maps directions link. Long routes are split into chained parts: part k ends where part k + 1 starts.
/// </summary>
/// <param name="Part">1-based part number ("Część 1 z 3").</param>
/// <param name="FromIndex">Index (into the route's points) of this part's origin.</param>
/// <param name="ToIndex">Index (into the route's points) of this part's destination.</param>
/// <param name="Url">Already URL-encoded; use as is (<c>System.Uri.ToString()</c> would unescape <c>%7C</c>).</param>
public sealed record NavigationLink(int Part, int TotalParts, int FromIndex, int ToIndex, string Url);
