namespace Kamada.Shared.Infrastructure.Persistence;

public static class LikePattern
{
    /// <summary>
    /// Builds a "contains" pattern for (I)LIKE with user input escaped, so <c>%</c> and <c>_</c> are literal.
    /// </summary>
    public static string Contains(string value) =>
        $"%{value.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal)}%";
}
