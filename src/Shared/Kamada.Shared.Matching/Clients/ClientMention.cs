namespace Kamada.Shared.Matching.Clients;

/// <summary>What was said about a client (any part may be missing). Names are expected in the nominative form.</summary>
public sealed record ClientMention(string? Name, string? Surname, string? PhoneNumber, string? Email)
{
    public bool IsEmpty =>
        string.IsNullOrWhiteSpace(Name) && string.IsNullOrWhiteSpace(Surname)
        && string.IsNullOrWhiteSpace(PhoneNumber) && string.IsNullOrWhiteSpace(Email);
}
