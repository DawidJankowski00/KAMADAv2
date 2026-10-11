using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Kamada.Shared.Matching.Drafts;

/// <summary>
/// The JSON the model is asked to produce (see <see cref="ExtractionPrompt.JsonSchema"/>). Small models do not always
/// follow every rule, so <see cref="ToDomain"/> cleans the values defensively instead of trusting them.
/// </summary>
internal sealed partial record LlmOrderResponse(
    [property: JsonPropertyName("client")] LlmClient? Client,
    [property: JsonPropertyName("address")] LlmAddress? Address,
    [property: JsonPropertyName("items")] IReadOnlyList<LlmItem>? Items,
    [property: JsonPropertyName("notes")] string? Notes)
{
    private const int MinPhoneDigits = 9;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        // Lenient about number/string mix-ups ("quantity": "6", "phone": 600100200).
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters = { new LenientStringConverter() },
    };

    private static readonly HashSet<string> NullWords = new(
        ["null", "none", "n/a", "brak", "unknown", "nieznany", "nieznana", "-"],
        StringComparer.OrdinalIgnoreCase);

    /// <summary>Parses the model's answer; <c>null</c> when it is not JSON of the expected shape.</summary>
    public static LlmOrderResponse? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<LlmOrderResponse>(StripCodeFence(text), Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public ExtractedOrder ToDomain()
    {
        var client = Client is null
            ? ExtractedClient.None
            : new ExtractedClient(PersonName(Client.Name), PersonName(Client.Surname), Phone(Client.Phone), Email(Client.Email));
        var address = Address is null ? ExtractedAddress.None : CleanAddress(Address);
        var lines = (Items ?? [])
            .Where(i => Clean(i.Product) is not null)
            .Select(i => new ExtractedLine(Clean(i.Product)!, Math.Max(1, i.Quantity ?? 1)))
            .ToList();

        return new ExtractedOrder(client, address, lines, Clean(Notes));
    }

    /// <summary>
    /// Small models often write the house number into the street ("Wojska Polskiego 70") or the apartment into the number
    /// ("50/59"). When the field they belong to is empty, they are moved there, so matching still gets street, building
    /// and apartment apart.
    /// </summary>
    private static ExtractedAddress CleanAddress(LlmAddress address)
    {
        var street = Clean(address.Street);
        var building = Compact(address.BuildingNumber);
        var apartment = Compact(address.ApartmentNumber);

        if (building is null && street is not null && StreetEndingWithNumber().Match(street) is { Success: true } trailing)
        {
            street = street[..trailing.Index].TrimEnd();
            building = trailing.Groups[1].Value.ToUpperInvariant();
        }

        if (apartment is null && building is not null && BuildingWithApartment().Match(building) is { Success: true } slash)
        {
            (building, apartment) = (slash.Groups[1].Value, slash.Groups[2].Value);
        }

        return new ExtractedAddress(street, building, apartment, Clean(address.City), PostalCode(address.PostalCode));
    }

    private static string? Compact(string? value) =>
        Clean(value)?.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant();

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) || NullWords.Contains(trimmed) ? null : trimmed;
    }

    /// <summary>"jan" → "Jan", "kowalska-nowak" → "Kowalska-Nowak"; names already capitalized are kept.</summary>
    private static string? PersonName(string? value)
    {
        var name = Clean(value);
        if (name is null || name.Any(char.IsUpper))
        {
            return name;
        }

        var chars = name.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (i == 0 || chars[i - 1] is ' ' or '-')
            {
                chars[i] = char.ToUpper(chars[i], CultureInfo.InvariantCulture);
            }
        }

        return new string(chars);
    }

    /// <summary>Digits with an optional leading <c>+</c>; shorter than a phone number → not a phone number.</summary>
    private static string? Phone(string? value)
    {
        var phone = Clean(value);
        if (phone is null)
        {
            return null;
        }

        var digits = string.Concat(phone.Where(char.IsAsciiDigit));
        if (digits.Length < MinPhoneDigits)
        {
            return null;
        }

        return phone.StartsWith('+') ? "+" + digits : digits;
    }

    private static string? Email(string? value)
    {
        var email = Clean(value)?.Replace(" ", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return email is not null && EmailPattern().IsMatch(email) ? email : null;
    }

    /// <summary>"80236" or "80 236" → "80-236"; anything else is dropped (the geocoder may fill it in).</summary>
    private static string? PostalCode(string? value)
    {
        var digits = string.Concat((Clean(value) ?? string.Empty).Where(char.IsAsciiDigit));
        return digits.Length == 5 ? $"{digits[..2]}-{digits[2..]}" : null;
    }

    private static string StripCodeFence(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var start = trimmed.IndexOf('\n', StringComparison.Ordinal);
        var end = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return start >= 0 && end > start ? trimmed[(start + 1)..end] : trimmed;
    }

    [GeneratedRegex(@"\s+(\d+[A-Za-z]?(?:/\d+[A-Za-z]?)?)$")]
    private static partial Regex StreetEndingWithNumber();

    [GeneratedRegex(@"^(\d+[A-Z]?)/(\d+[A-Z]?)$")]
    private static partial Regex BuildingWithApartment();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}

internal sealed record LlmClient(
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("surname")] string? Surname,
    [property: JsonPropertyName("phone")] string? Phone,
    [property: JsonPropertyName("email")] string? Email);

internal sealed record LlmAddress(
    [property: JsonPropertyName("street")] string? Street,
    [property: JsonPropertyName("buildingNumber")] string? BuildingNumber,
    [property: JsonPropertyName("apartmentNumber")] string? ApartmentNumber,
    [property: JsonPropertyName("city")] string? City,
    [property: JsonPropertyName("postalCode")] string? PostalCode);

internal sealed record LlmItem(
    [property: JsonPropertyName("product")] string? Product,
    [property: JsonPropertyName("quantity")] int? Quantity);

/// <summary>Reads numbers and booleans into string properties as their raw text.</summary>
internal sealed class LenientStringConverter : JsonConverter<string>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.String => reader.GetString(),
        JsonTokenType.Null => null,
        JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False => System.Text.Encoding.UTF8.GetString(reader.ValueSpan),
        _ => throw new JsonException($"Expected a string, got {reader.TokenType}."),
    };

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) => writer.WriteStringValue(value);
}
