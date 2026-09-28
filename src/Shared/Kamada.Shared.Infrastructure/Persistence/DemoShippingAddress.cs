namespace Kamada.Shared.Infrastructure.Persistence;

/// <summary>A seeded client's active shipping address (see <see cref="DemoSeedData"/>).</summary>
public sealed record DemoShippingAddress(Guid ClientId, Guid AddressId, DemoRegion Region);
