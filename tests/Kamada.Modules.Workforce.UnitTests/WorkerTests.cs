using Kamada.Modules.Workforce.Domain.Workers;

namespace Kamada.Modules.Workforce.UnitTests;

public sealed class WorkerTests
{
    private static Worker CreateCourier() =>
        Worker.Create("  Kurier1 ", "hash", " Jan ", " Kowalski ", " +48 600 000 000 ", WorkerRole.Courier);

    [Fact]
    public void Create_NormalizesUsernameAndTrimsFields()
    {
        var worker = CreateCourier();

        worker.Username.ShouldBe("kurier1");
        worker.Name.ShouldBe("Jan");
        worker.Surname.ShouldBe("Kowalski");
        worker.PhoneNumber.ShouldBe("+48 600 000 000");
        worker.IsActive.ShouldBeTrue();
        worker.Id.Version.ShouldBe(7);
    }

    [Fact]
    public void Deactivate_ByAnotherWorker_DeactivatesAccount()
    {
        var worker = CreateCourier();

        var result = worker.Deactivate(deactivatedBy: Guid.NewGuid());

        result.IsSuccess.ShouldBeTrue();
        worker.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Deactivate_OwnAccount_FailsAndKeepsAccountActive()
    {
        var worker = CreateCourier();

        var result = worker.Deactivate(deactivatedBy: worker.Id);

        result.Error.ShouldBe(WorkerErrors.CannotDeactivateSelf);
        worker.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void ChangeRole_OwnRole_Fails()
    {
        var worker = CreateCourier();

        var result = worker.ChangeRole(WorkerRole.Admin, changedBy: worker.Id);

        result.Error.ShouldBe(WorkerErrors.CannotChangeOwnRole);
        worker.Role.ShouldBe(WorkerRole.Courier);
    }

    [Fact]
    public void ChangeRole_SameRoleByOwner_IsNoOp()
    {
        var worker = CreateCourier();

        worker.ChangeRole(WorkerRole.Courier, changedBy: worker.Id).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ChangeRole_ByAnotherWorker_ChangesRole()
    {
        var worker = CreateCourier();

        worker.ChangeRole(WorkerRole.Admin, changedBy: Guid.NewGuid()).IsSuccess.ShouldBeTrue();

        worker.Role.ShouldBe(WorkerRole.Admin);
    }
}
