namespace Kamada.ArchitectureTests;

/// <summary>
/// Frontends talk to the backend only over HTTP: they may reference the HTTP contracts and the shared UI libraries,
/// never a module or the API host.
/// </summary>
public sealed class FrontendTests
{
    private static readonly string[] AllowedFrontendReferences = ["Kamada.Shared.Contracts", "Kamada.Web.Shared", "Kamada.Web.Admin"];

    [Theory]
    [InlineData("Kamada.AdminPanel")]
    [InlineData("Kamada.MobileApp")]
    public void Frontend_ReferencesOnlyContractsAndSharedUi(string project)
    {
        var path = Path.Combine(RepositoryPaths.Root, "src", "Web", project, $"{project}.csproj");

        ProjectReferences.Of(path).ShouldAllBe(reference => AllowedFrontendReferences.Contains(reference));
    }

    [Fact]
    public void SharedUiLibrary_ReferencesOnlyHttpContractsAndPureMatching()
    {
        var path = Path.Combine(RepositoryPaths.Root, "src", "Web", "Kamada.Web.Shared", "Kamada.Web.Shared.csproj");

        ProjectReferences.Of(path).ShouldBe(["Kamada.Shared.Contracts", "Kamada.Shared.Matching"], ignoreOrder: true);
    }

    [Fact]
    public void SharedAdminPages_ReferenceOnlyHttpContractsAndSharedUi()
    {
        var path = Path.Combine(RepositoryPaths.Root, "src", "Web", "Kamada.Web.Admin", "Kamada.Web.Admin.csproj");

        ProjectReferences.Of(path).ShouldBe(["Kamada.Shared.Contracts", "Kamada.Web.Shared"], ignoreOrder: true);
    }

    [Fact]
    public void MatchingLibrary_IsPure_ReferencesOnlyHttpContractsAndNoFramework()
    {
        var path = Path.Combine(RepositoryPaths.Root, "src", "Shared", "Kamada.Shared.Matching", "Kamada.Shared.Matching.csproj");

        ProjectReferences.Of(path).ShouldBe(["Kamada.Shared.Contracts"]);
        File.ReadAllText(path).ShouldNotContain("FrameworkReference");
        File.ReadAllText(path).ShouldNotContain("PackageReference");
    }
}
