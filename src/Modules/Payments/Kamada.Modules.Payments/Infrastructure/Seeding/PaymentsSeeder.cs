using Kamada.Modules.Payments.Application;
using Kamada.Modules.Payments.Infrastructure.Persistence;
using Kamada.Shared.Contracts.Payments;
using Kamada.Shared.Infrastructure.Persistence;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.Modules.Payments.Infrastructure.Seeding;

/// <summary>
/// Pays the demo orders through <see cref="PaymentService"/> (which marks them paid), replayed with a
/// <see cref="SeedClock"/> a few minutes after each order was placed. Mix of card, cash and confirmed transfers;
/// one card is declined before a second attempt succeeds, the newest orders are unpaid or wait for a transfer.
/// </summary>
internal sealed class PaymentsSeeder(PaymentsDbContext db, IServiceProvider services, DemoSeedData seedData, TimeProvider clock) : IDataSeeder
{
    private static readonly TimeSpan JustPlaced = TimeSpan.FromHours(3);

    public int Order => SeedOrder.Payments;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (seedData.Orders.Count == 0 || await db.Payments.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = clock.GetUtcNow();
        var seedClock = new SeedClock(now);
        var gateway = ActivatorUtilities.CreateInstance<SimulatedPaymentGateway>(services, seedClock);
        var payments = ActivatorUtilities.CreateInstance<PaymentService>(services, gateway, seedClock);
        var random = new Random(40);
        var registered = new List<(Guid PaymentId, DateTimeOffset At)>();

        async Task<PaymentDto> RegisterAsync(Guid orderId, DateTimeOffset at, PaymentProvider provider, bool decline = false)
        {
            seedClock.Now = at;
            var payment = Ensure(await payments.RegisterAsync(orderId, new RegisterPaymentRequest(provider, decline), cancellationToken));
            registered.Add((payment.Id, at));
            return payment;
        }

        for (var i = 0; i < seedData.Orders.Count; i++)
        {
            var (orderId, placedAt) = seedData.Orders[i];
            var at = placedAt.AddMinutes(random.Next(1, 15));
            if (now - placedAt < JustPlaced)
            {
                // Freshly placed: waiting for a bank transfer or not paid yet.
                if (i % 2 == 0)
                {
                    await RegisterAsync(orderId, at, PaymentProvider.Transfer);
                }

                continue;
            }

            if (i % 11 == 5)
            {
                continue; // Never paid – cancelled after two days by the packing seeder.
            }

            var roll = random.Next(100);
            if (i == 3)
            {
                await RegisterAsync(orderId, at, PaymentProvider.Card, decline: true);
                await RegisterAsync(orderId, at.AddMinutes(3), PaymentProvider.Card);
            }
            else if (roll < 55)
            {
                await RegisterAsync(orderId, at, PaymentProvider.Card);
            }
            else if (roll < 80)
            {
                await RegisterAsync(orderId, at, PaymentProvider.Cash);
            }
            else
            {
                var transfer = await RegisterAsync(orderId, at, PaymentProvider.Transfer);
                var arrived = at.AddMinutes(random.Next(60, 300));
                seedClock.Now = arrived < now ? arrived : now;
                Ensure(await payments.ConfirmTransferAsync(transfer.Id, cancellationToken));
            }
        }

        // created_at is stamped on save; move each payment back to the moment it was registered.
        foreach (var (paymentId, at) in registered)
        {
            await db.Payments.Where(p => p.Id == paymentId).ExecuteUpdateAsync(s => s.SetProperty(p => p.CreatedAt, at), cancellationToken);
        }

        // The update changed xmin: later seeders in this scope must load fresh entities.
        db.ChangeTracker.Clear();
    }

    private static PaymentDto Ensure(Result<PaymentDto> result) =>
        result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException($"Demo payment failed: {result.Error.Code} – {result.Error.Description}");
}
