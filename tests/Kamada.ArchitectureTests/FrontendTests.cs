namespace Kamada.ArchitectureTests;

/// <summary>
/// Frontends talk to the backend only over HTTP: they may reference the HTTP contracts and the shared UI library,
/// never a module or the API host.
/// </summary>
public sealed class FrontendTests
{
    private static readonly string[] AllowedFrontendReferences = ["Kamada.Shared.Contracts", "Kamada.Web.Shared"];

    [Theory]
    [InlineData("Kamada.AdminPanel")]
    [InlineData("Kamada.CourierApp")]
    public void Frontend_ReferencesOnlyContractsAndSharedUi(string project)
    {
        var path = Path.Combine(RepositoryPaths.Root, "src", "Web", project, $"{project}.csproj");

        ProjectReferences.Of(path).ShouldAllBe(reference => AllowedFrontendReferences.Contains(reference));
    }

    [Fact]
    public void SharedUiLibrary_ReferencesOnlyHttpContracts()
    {
        var path = Path.Combine(RepositoryPaths.Root, "src", "Web", "Kamada.Web.Shared", "Kamada.Web.Shared.csproj");

        ProjectReferences.Of(path).ShouldBe(["Kamada.Shared.Contracts"]);
    }
}
