namespace Kamada.Modules.Workforce.Domain.Auth;

/// <summary>
/// What the worker sees in the session list to recognise a device: the browser's User-Agent and the client IP of the
/// request that issued the refresh token. Both are optional and cut to the column length.
/// </summary>
internal sealed record SessionClient
{
    public const int UserAgentMaxLength = 256;
    public const int IpAddressMaxLength = 45;

    public static readonly SessionClient Unknown = new(null, null);

    public SessionClient(string? userAgent, string? ipAddress)
    {
        UserAgent = Cut(userAgent, UserAgentMaxLength);
        IpAddress = Cut(ipAddress, IpAddressMaxLength);
    }

    public string? UserAgent { get; }

    public string? IpAddress { get; }

    private static string? Cut(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }
}
