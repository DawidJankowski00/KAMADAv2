using Kamada.Shared.Contracts.Catalog;
using Kamada.Web.Shared.Api;

namespace Kamada.AdminPanel.Services;

/// <summary>Flattened category tree for selects: "Spożywcze › Kawa".</summary>
public sealed record CategoryOption(Guid Id, string Path, int Depth);

public static class CategoryOptions
{
    public static async Task<IReadOnlyList<CategoryOption>> LoadAsync(ApiClient api)
    {
        var result = await api.GetAsync<List<CategoryNodeDto>>("api/categories");
        return result.IsSuccess ? Flatten(result.Value) : [];
    }

    public static IReadOnlyList<CategoryOption> Flatten(IEnumerable<CategoryNodeDto> nodes)
    {
        var options = new List<CategoryOption>();

        void Visit(CategoryNodeDto node, string prefix, int depth)
        {
            var path = prefix.Length == 0 ? node.Name : $"{prefix} › {node.Name}";
            options.Add(new CategoryOption(node.Id, path, depth));
            foreach (var child in node.Children)
            {
                Visit(child, path, depth + 1);
            }
        }

        foreach (var node in nodes)
        {
            Visit(node, string.Empty, 0);
        }

        return options;
    }
}
