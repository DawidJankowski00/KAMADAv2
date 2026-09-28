namespace Kamada.Shared.Infrastructure.Persistence;

/// <summary>
/// Settable clock for replaying demo history through the real use cases: a seeder creates the handler with this
/// clock (<c>ActivatorUtilities.CreateInstance</c>) and moves it to the moment each step "happened"
/// (payment at 10:12 yesterday, stop delivered at 09:47, ...).
/// </summary>
public sealed class SeedClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override TimeZoneInfo LocalTimeZone => DemoSeedData.TimeZone;

    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
}
