using Kamada.Shared.Contracts.Assistant;

namespace Kamada.Shared.Matching.Clients;

/// <summary>An active client similar to a <see cref="ClientMention"/>; <see cref="Score"/> is 0 … 1 (1 = exact phone/e-mail).</summary>
public sealed record ClientCandidate(
    Guid Id,
    string Name,
    string Surname,
    string Email,
    string PhoneNumber,
    double Score,
    ClientMatchReason Reason);
