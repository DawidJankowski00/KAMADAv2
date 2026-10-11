using Kamada.Modules.Workforce.Application;
using Kamada.Modules.Workforce.Domain.Workers;

namespace Kamada.Modules.Workforce.UnitTests;

public sealed class WorkerMappingsTests
{
    [Fact]
    public void ToContract_EveryRole_RoundTripsWithTheSameName()
    {
        foreach (var role in Enum.GetValues<WorkerRole>())
        {
            var contract = role.ToContract();

            contract.ToString().ShouldBe(role.ToString());
            contract.ToDomain().ShouldBe(role);
        }
    }

    [Fact]
    public void ToRoleName_EveryRole_MatchesTheRoleClaimConstant()
    {
        var claimRoles = typeof(Kamada.Shared.Contracts.Auth.Roles).GetFields().Select(f => (string)f.GetRawConstantValue()!);

        Enum.GetValues<WorkerRole>().Select(r => r.ToRoleName()).ShouldBe(claimRoles, ignoreOrder: true);
    }
}
