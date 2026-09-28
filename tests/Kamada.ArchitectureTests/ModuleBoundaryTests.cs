using NetArchTest.Rules;

namespace Kamada.ArchitectureTests;

public sealed class ModuleBoundaryTests
{
    public static TheoryData<string> ModuleNames => Modules.Names;

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void ModuleProject_ReferencesOnlyContractsOfOtherModules(string module)
    {
        var references = ProjectReferences.Of(ProjectReferences.ModuleProject(module));

        var forbidden = references
            .Where(r => r.StartsWith("Kamada.Modules.", StringComparison.Ordinal))
            .Where(r => !r.EndsWith(".Contracts", StringComparison.Ordinal));

        forbidden.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void ContractsProject_DoesNotReferenceAnyModuleImplementation(string module)
    {
        var references = ProjectReferences.Of(ProjectReferences.ModuleProject(module, contracts: true));

        references
            .Where(r => r.StartsWith("Kamada.Modules.", StringComparison.Ordinal))
            .ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void ModuleTypes_DoNotDependOnInternalsOfOtherModules(string module)
    {
        var current = Modules.Get(module);
        var otherInternals = Modules.All
            .Where(m => m.Name != module)
            .SelectMany(m => m.InternalNamespaces)
            .ToArray();

        var result = Types.InAssembly(current.Assembly)
            .ShouldNot()
            .HaveDependencyOnAny(otherInternals)
            .GetResult();

        (result.FailingTypeNames ?? []).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void ModuleImplementation_ExposesOnlyModuleEntryPointPublicly(string module)
    {
        var current = Modules.Get(module);

        // EF Core generates migrations as public types; they carry no behaviour other modules could use.
        var publicTypes = current.Assembly.GetExportedTypes()
            .Where(t => t.Namespace?.EndsWith(".Migrations", StringComparison.Ordinal) != true)
            .Select(t => t.FullName);

        publicTypes.ShouldBe([$"{current.RootNamespace}.{module}Module"]);
    }
}
