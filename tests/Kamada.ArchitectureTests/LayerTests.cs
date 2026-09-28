using NetArchTest.Rules;

namespace Kamada.ArchitectureTests;

public sealed class LayerTests
{
    public static TheoryData<string> ModuleNames => Modules.Names;

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Domain_DoesNotDependOnOuterLayersOrFrameworks(string module)
    {
        var current = Modules.Get(module);

        var result = Types.InAssembly(current.Assembly)
            .That()
            .ResideInNamespace($"{current.RootNamespace}.Domain")
            .ShouldNot()
            .HaveDependencyOnAny(
                $"{current.RootNamespace}.Application",
                $"{current.RootNamespace}.Infrastructure",
                $"{current.RootNamespace}.Api",
                "Microsoft.EntityFrameworkCore",
                "Microsoft.AspNetCore",
                "Npgsql")
            .GetResult();

        (result.FailingTypeNames ?? []).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Application_DoesNotDependOnInfrastructureOrApi(string module)
    {
        var current = Modules.Get(module);

        var result = Types.InAssembly(current.Assembly)
            .That()
            .ResideInNamespace($"{current.RootNamespace}.Application")
            .ShouldNot()
            .HaveDependencyOnAny($"{current.RootNamespace}.Infrastructure", $"{current.RootNamespace}.Api")
            .GetResult();

        (result.FailingTypeNames ?? []).ShouldBeEmpty();
    }
}
