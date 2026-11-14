using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Workforce.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.IntegrationTests.Persistence;

public sealed class DataProtectionKeyTests(KamadaApiFactory factory)
{
    [Fact]
    public async Task ProtectedData_CanBeReadByAnotherApiInstance_BecauseKeysLiveInTheDatabase()
    {
        var protector = factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("tests");
        var secret = protector.Protect("totp-secret");

        // A second host has its own in-memory key ring cache: it can only decrypt by loading the keys from the database.
        await using var otherInstance = factory.WithWebHostBuilder(_ => { });
        var unprotected = otherInstance.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("tests").Unprotect(secret);

        unprotected.ShouldBe("totp-secret");
        await using var scope = factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<WorkforceDbContext>().DataProtectionKeys
            .CountAsync(TestContext.Current.CancellationToken)).ShouldBeGreaterThan(0);
    }
}
