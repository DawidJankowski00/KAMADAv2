namespace Kamada.ArchitectureTests;

public sealed class FrontendTests
{
    [Theory]
    [InlineData("Kamada.AdminPanel")]
    [InlineData("Kamada.CourierApp")]
    public void Frontend_ReferencesOnlySharedContracts(string project)
    {
        var path = Path.Combine(RepositoryPaths.Root, "src", "Web", project, $"{project}.csproj");

        ProjectReferences.Of(path).ShouldBe(["Kamada.Shared.Contracts"]);
    }
}
