using System.Xml.Linq;

namespace Kamada.ArchitectureTests;

/// <summary>
/// Reads &lt;ProjectReference&gt; items straight from .csproj files. Compiler-level assembly references
/// drop unused references, so checking project files catches violations even before any code uses them.
/// </summary>
internal static class ProjectReferences
{
    public static IReadOnlyList<string> Of(string projectPath)
    {
        var document = XDocument.Load(projectPath);
        return document.Descendants("ProjectReference")
            .Select(e => Path.GetFileNameWithoutExtension(e.Attribute("Include")!.Value.Replace('\\', '/')))
            .ToList();
    }

    public static string ModuleProject(string module, bool contracts = false)
    {
        var name = contracts ? $"Kamada.Modules.{module}.Contracts" : $"Kamada.Modules.{module}";
        return Path.Combine(RepositoryPaths.Root, "src", "Modules", module, name, $"{name}.csproj");
    }
}
