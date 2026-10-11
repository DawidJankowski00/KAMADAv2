namespace Kamada.Web.Shared.Api;

/// <summary>Result of a conditional GET (<see cref="ApiClient.GetIfChangedAsync{T}"/>).</summary>
/// <param name="Value">The new data; default when <paramref name="NotModified"/>.</param>
/// <param name="ETag">The tag of the data the caller now has.</param>
public sealed record Tagged<T>(T? Value, string? ETag, bool NotModified);
