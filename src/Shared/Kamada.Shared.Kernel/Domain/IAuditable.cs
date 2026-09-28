namespace Kamada.Shared.Kernel.Domain;

/// <summary>
/// Entities with timestamps maintained by the persistence layer (EF Core interceptor),
/// so domain code never sets them manually.
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }

    DateTimeOffset? UpdatedAt { get; }
}
