namespace Kamada.Web.Shared.Offline;

/// <summary>A downloaded list kept on the device.</summary>
/// <param name="ETag">Tag of the list, sent back as <c>If-None-Match</c> on the next refresh.</param>
/// <param name="CheckedAt">When the server last confirmed the list (downloaded or answered "not modified").</param>
public sealed record LookupSnapshot<T>(IReadOnlyList<T> Items, string? ETag, DateTimeOffset CheckedAt);
