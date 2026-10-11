using Kamada.Modules.Workforce.Domain.Workers;

namespace Kamada.Modules.Workforce.UnitTests;

public sealed class WorkerTests
{
    private static Worker CreateWorker(params WorkerRole[] roles) =>
        Worker.Create("  Kurier1 ", "hash", " Jan ", " Kowalski ", " +48 600 000 000 ", roles).Value;

    [Fact]
    public void Create_NormalizesUsernameAndTrimsFields()
    {
        var worker = CreateWorker(WorkerRole.Courier);

        worker.Username.ShouldBe("kurier1");
        worker.Name.ShouldBe("Jan");
        worker.Surname.ShouldBe("Kowalski");
        worker.PhoneNumber.ShouldBe("+48 600 000 000");
        worker.IsActive.ShouldBeTrue();
        worker.Id.Version.ShouldBe(7);
    }

    [Fact]
    public void Create_SeveralRoles_StoresThemInEnumOrder()
    {
        var worker = CreateWorker(WorkerRole.Courier, WorkerRole.Admin);

        worker.Roles.ShouldBe([WorkerRole.Admin, WorkerRole.Courier]);
        worker.HasRole(WorkerRole.Admin).ShouldBeTrue();
        worker.HasRole(WorkerRole.Courier).ShouldBeTrue();
    }

    [Fact]
    public void Create_WithoutRoles_Fails()
    {
        var result = Worker.Create("kurier1", "hash", "Jan", "Kowalski", "+48 600 000 000", []);

        result.Error.ShouldBe(WorkerErrors.RolesRequired);
    }

    [Fact]
    public void Create_WithDuplicateRoles_Fails()
    {
        var result = Worker.Create("kurier1", "hash", "Jan", "Kowalski", "+48 600 000 000", [WorkerRole.Courier, WorkerRole.Courier]);

        result.Error.ShouldBe(WorkerErrors.DuplicateRoles);
    }

    [Fact]
    public void Deactivate_ByAnotherWorker_DeactivatesAccount()
    {
        var worker = CreateWorker(WorkerRole.Courier);

        var result = worker.Deactivate(deactivatedBy: Guid.NewGuid());

        result.IsSuccess.ShouldBeTrue();
        worker.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Deactivate_OwnAccount_FailsAndKeepsAccountActive()
    {
        var worker = CreateWorker(WorkerRole.Courier);

        var result = worker.Deactivate(deactivatedBy: worker.Id);

        result.Error.ShouldBe(WorkerErrors.CannotDeactivateSelf);
        worker.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void SetRoles_ByAnotherWorker_ReplacesRoles()
    {
        var worker = CreateWorker(WorkerRole.Courier);

        worker.SetRoles([WorkerRole.Admin, WorkerRole.Courier], changedBy: Guid.NewGuid()).IsSuccess.ShouldBeTrue();

        worker.Roles.ShouldBe([WorkerRole.Admin, WorkerRole.Courier]);
    }

    [Fact]
    public void SetRoles_Empty_FailsAndKeepsRoles()
    {
        var worker = CreateWorker(WorkerRole.Courier);

        var result = worker.SetRoles([], changedBy: Guid.NewGuid());

        result.Error.ShouldBe(WorkerErrors.RolesRequired);
        worker.Roles.ShouldBe([WorkerRole.Courier]);
    }

    [Fact]
    public void SetRoles_WithDuplicates_Fails()
    {
        var worker = CreateWorker(WorkerRole.Courier);

        var result = worker.SetRoles([WorkerRole.Admin, WorkerRole.Admin], changedBy: Guid.NewGuid());

        result.Error.ShouldBe(WorkerErrors.DuplicateRoles);
    }

    [Fact]
    public void SetRoles_RemovingOwnAdminRole_FailsAndKeepsRoles()
    {
        var worker = CreateWorker(WorkerRole.Admin, WorkerRole.Courier);

        var result = worker.SetRoles([WorkerRole.Courier], changedBy: worker.Id);

        result.Error.ShouldBe(WorkerErrors.CannotRemoveOwnAdminRole);
        worker.Roles.ShouldBe([WorkerRole.Admin, WorkerRole.Courier]);
    }

    [Fact]
    public void SetRoles_OwnAccountKeepingAdmin_AddsRole()
    {
        var worker = CreateWorker(WorkerRole.Admin);

        worker.SetRoles([WorkerRole.Admin, WorkerRole.Courier], changedBy: worker.Id).IsSuccess.ShouldBeTrue();

        worker.Roles.ShouldBe([WorkerRole.Admin, WorkerRole.Courier]);
    }

    [Fact]
    public void SetRoles_OwnerRemovingAdminFromAnotherWorker_Succeeds()
    {
        var worker = CreateWorker(WorkerRole.Admin, WorkerRole.Courier);

        worker.SetRoles([WorkerRole.Courier], changedBy: Guid.NewGuid()).IsSuccess.ShouldBeTrue();

        worker.Roles.ShouldBe([WorkerRole.Courier]);
    }
}
