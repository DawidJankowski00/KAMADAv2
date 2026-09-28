namespace Kamada.Shared.Infrastructure.Persistence;

/// <summary>A seeded order, the (past) moment it was placed and the area it is delivered to (see <see cref="DemoSeedData"/>).</summary>
public sealed record DemoOrder(Guid OrderId, DateTimeOffset PlacedAt, DemoRegion Region);
