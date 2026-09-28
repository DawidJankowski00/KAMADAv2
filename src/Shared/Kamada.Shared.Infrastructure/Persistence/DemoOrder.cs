namespace Kamada.Shared.Infrastructure.Persistence;

/// <summary>A seeded order and the (past) moment it was placed (see <see cref="DemoSeedData"/>).</summary>
public sealed record DemoOrder(Guid OrderId, DateTimeOffset PlacedAt);
