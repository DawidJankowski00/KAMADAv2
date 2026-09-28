using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kamada.Web.Shared.Api;

/// <summary>JSON conventions shared with the API: web defaults (camelCase) and enums as strings.</summary>
public static class KamadaJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };
}
