namespace Kamada.Shared.Matching.Clients;

/// <summary>Searchable projection of an active client.</summary>
public sealed record ClientRow(Guid Id, string Name, string Surname, string Email, string PhoneNumber);
