using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Catalog.Infrastructure.Persistence;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Modules.Workforce.Infrastructure.Persistence;
using Kamada.Shared.Infrastructure.Persistence;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.IntegrationTests.Persistence;

/// <summary>
/// Verifies the mechanism behind cross-module consistency: changes saved by different module contexts
/// inside <see cref="IUnitOfWork"/> are committed or rolled back together.
/// </summary>
public sealed class UnitOfWorkTests(KamadaApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ChangesOfTwoModules_AreCommittedOrRolledBackTogether(bool succeed)
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var product = await admin.CreateProductAsync(stock: 10);
        var worker = await factory.CreateWorkerAsync(WorkerRole.Courier);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            var workforce = scope.ServiceProvider.GetRequiredService<WorkforceDbContext>();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            await unitOfWork.ExecuteAsync(
                async ct =>
                {
                    (await catalog.Products.SingleAsync(p => p.Id == product.Id, ct)).SetStock(1);
                    await catalog.SaveChangesAsync(ct);

                    (await workforce.Workers.SingleAsync(w => w.Id == worker.Id, ct)).UpdateProfile("Zmienione", "Nazwisko", "+48 600 000 001");
                    await workforce.SaveChangesAsync(ct);

                    return succeed ? Result.Success() : Error.Failure("Test.Failure", "Simulated failure after both saves.");
                },
                Ct);
        }

        await using var verify = factory.Services.CreateAsyncScope();
        var stock = await verify.ServiceProvider.GetRequiredService<CatalogDbContext>()
            .Products.Where(p => p.Id == product.Id).Select(p => p.StockQuantity).SingleAsync(Ct);
        var name = await verify.ServiceProvider.GetRequiredService<WorkforceDbContext>()
            .Workers.Where(w => w.Id == worker.Id).Select(w => w.Name).SingleAsync(Ct);

        stock.ShouldBe(succeed ? 1 : 10);
        name.ShouldBe(succeed ? "Zmienione" : "Test");
    }
}
