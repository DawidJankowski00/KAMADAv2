using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Shared.Contracts.Auth;
using Kamada.Shared.Contracts.Common;
using Kamada.Shared.Contracts.Workforce;
using DomainRole = Kamada.Modules.Workforce.Domain.Workers.WorkerRole;

namespace Kamada.IntegrationTests.Workforce;

public sealed class WorkerManagementTests(KamadaApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static CreateWorkerRequest NewCourierRequest() => new(
        $"courier-{Guid.NewGuid():N}"[..20],
        "Courier123",
        "Adam",
        "Zieliński",
        "+48 700 800 900",
        [WorkerRole.Courier]);

    [Fact]
    public async Task Workers_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().GetAsync("/api/workers", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Workers_AsCourier_Returns403()
    {
        var (courier, _) = await factory.CreateSignedInClientAsync(DomainRole.Courier);

        var response = await courier.GetAsync("/api/workers", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_AsAdmin_Returns201AndNewWorkerCanSignIn()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(DomainRole.Admin);
        var request = NewCourierRequest();

        var response = await admin.PostJsonAsync("/api/workers", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull();
        var created = await response.ReadAsAsync<WorkerDto>();
        created.Roles.ShouldBe([WorkerRole.Courier]);
        created.IsActive.ShouldBeTrue();

        var tokens = await factory.CreateClient().LoginAsync(request.Username, request.Password);
        tokens.User.Roles.ShouldBe([Roles.Courier]);
    }

    [Fact]
    public async Task Create_WithTakenUsername_Returns409()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(DomainRole.Admin);
        var request = NewCourierRequest();
        (await admin.PostJsonAsync("/api/workers", request)).EnsureSuccessStatusCode();

        var duplicate = await admin.PostJsonAsync("/api/workers", request with { Username = request.Username.ToUpperInvariant() });

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await duplicate.ReadProblemCodeAsync()).ShouldBe("Workers.UsernameTaken");
    }

    [Fact]
    public async Task Create_WithWeakPassword_Returns400()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(DomainRole.Admin);

        var response = await admin.PostJsonAsync("/api/workers", NewCourierRequest() with { Password = "weak" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_WithSearchAndRoleFilter_ReturnsMatchingWorkers()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(DomainRole.Admin);
        var request = NewCourierRequest() with { Surname = $"Unikalny{Guid.NewGuid():N}"[..20] };
        (await admin.PostJsonAsync("/api/workers", request)).EnsureSuccessStatusCode();

        var response = await admin.GetAsync($"/api/workers?search={request.Surname}&role=Courier", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var page = await response.ReadAsAsync<PagedResult<WorkerDto>>();
        page.TotalCount.ShouldBe(1);
        page.Items.Single().Username.ShouldBe(request.Username);
    }

    [Fact]
    public async Task Create_WithSeveralRoles_ReturnsAllRoles()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(DomainRole.Admin);

        var response = await admin.PostJsonAsync("/api/workers", NewCourierRequest() with { Roles = [WorkerRole.Courier, WorkerRole.Admin] });

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await response.ReadAsAsync<WorkerDto>()).Roles.ShouldBe([WorkerRole.Admin, WorkerRole.Courier]);
    }

    [Fact]
    public async Task Create_Seller_NewWorkerSignsInWithSellerRole()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(DomainRole.Admin);
        var request = NewCourierRequest() with { Roles = [WorkerRole.Seller] };

        var response = await admin.PostJsonAsync("/api/workers", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await response.ReadAsAsync<WorkerDto>()).Roles.ShouldBe([WorkerRole.Seller]);
        var tokens = await factory.CreateClient().LoginAsync(request.Username, request.Password);
        tokens.User.Roles.ShouldBe([Roles.Seller]);
    }

    [Fact]
    public async Task Create_WithoutRoles_Returns400()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(DomainRole.Admin);

        var response = await admin.PostJsonAsync("/api/workers", NewCourierRequest() with { Roles = [] });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_Roles_ReplacesRoles()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(DomainRole.Admin);
        var courier = await factory.CreateWorkerAsync(DomainRole.Courier);

        var response = await admin.PutJsonAsync(
            $"/api/workers/{courier.Id}",
            new UpdateWorkerRequest("Test", "Courier", "+48 600 000 000", [WorkerRole.Admin, WorkerRole.Courier]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsAsync<WorkerDto>()).Roles.ShouldBe([WorkerRole.Admin, WorkerRole.Courier]);
    }

    [Fact]
    public async Task Update_RemovingOwnAdminRole_Returns409()
    {
        var (admin, self) = await factory.CreateSignedInClientAsync(DomainRole.Admin);

        var response = await admin.PutJsonAsync($"/api/workers/{self.Id}", new UpdateWorkerRequest("Test", "Admin", "+48 600 000 000", [WorkerRole.Courier]));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadProblemCodeAsync()).ShouldBe("Workers.CannotRemoveOwnAdminRole");
    }

    [Fact]
    public async Task Update_AddingRoleToOwnAccount_Succeeds()
    {
        var (admin, self) = await factory.CreateSignedInClientAsync(DomainRole.Admin);

        var response = await admin.PutJsonAsync(
            $"/api/workers/{self.Id}",
            new UpdateWorkerRequest("Test", "Admin", "+48 600 000 000", [WorkerRole.Admin, WorkerRole.Courier]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsAsync<WorkerDto>()).Roles.ShouldBe([WorkerRole.Admin, WorkerRole.Courier]);
    }

    [Theory]
    [InlineData(nameof(WorkerRole.Admin))]
    [InlineData(nameof(WorkerRole.Courier))]
    public async Task List_FilterByRole_IncludesWorkersWithSeveralRoles(string role)
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(DomainRole.Admin);
        var worker = await factory.CreateWorkerAsync([DomainRole.Admin, DomainRole.Courier]);

        var page = await (await admin.GetAsync($"/api/workers?search={worker.Username}&role={role}", Ct)).ReadAsAsync<PagedResult<WorkerDto>>();

        page.Items.ShouldHaveSingleItem().Id.ShouldBe(worker.Id);
    }

    [Fact]
    public async Task Deactivate_RevokesSessionsAndBlocksLogin()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(DomainRole.Admin);
        var courier = await factory.CreateWorkerAsync(DomainRole.Courier);
        var courierTokens = await factory.CreateClient().LoginAsync(courier.Username, courier.Password);

        var response = await admin.PostAsync($"/api/workers/{courier.Id}/deactivate", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var anonymous = factory.CreateClient();
        (await anonymous.PostJsonAsync("/api/auth/login", new LoginRequest(courier.Username, courier.Password)))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PostJsonAsync("/api/auth/refresh", new RefreshTokenRequest(courierTokens.RefreshToken)))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Deactivate_OwnAccount_Returns409()
    {
        var (admin, self) = await factory.CreateSignedInClientAsync(DomainRole.Admin);

        var response = await admin.PostAsync($"/api/workers/{self.Id}/deactivate", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadProblemCodeAsync()).ShouldBe("Workers.CannotDeactivateSelf");
    }

    [Fact]
    public async Task ResetPassword_OldPasswordStopsWorkingAndNewOneWorks()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(DomainRole.Admin);
        var courier = await factory.CreateWorkerAsync(DomainRole.Courier);

        var response = await admin.PostJsonAsync($"/api/workers/{courier.Id}/reset-password", new ResetPasswordRequest("Changed123"));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var anonymous = factory.CreateClient();
        (await anonymous.PostJsonAsync("/api/auth/login", new LoginRequest(courier.Username, courier.Password)))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.LoginAsync(courier.Username, "Changed123")).User.Id.ShouldBe(courier.Id);
    }

    [Fact]
    public async Task Get_UnknownWorker_Returns404()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(DomainRole.Admin);

        var response = await admin.GetAsync($"/api/workers/{Guid.NewGuid()}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
