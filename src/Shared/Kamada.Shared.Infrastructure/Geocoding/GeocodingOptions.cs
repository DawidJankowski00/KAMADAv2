using System.ComponentModel.DataAnnotations;

namespace Kamada.Shared.Infrastructure.Geocoding;

public enum GeocodingProvider
{
    /// <summary>OpenStreetMap Nominatim – no API key, max 1 request/second (usage policy).</summary>
    Nominatim,

    /// <summary>Google Geocoding API – requires <see cref="GoogleGeocodingOptions.ApiKey"/>.</summary>
    Google,

    /// <summary>Geocoding disabled; admins set pins manually.</summary>
    None,
}

public sealed class GeocodingOptions : IValidatableObject
{
    public const string SectionName = "Geocoding";

    public GeocodingProvider Provider { get; init; } = GeocodingProvider.Nominatim;

    public TimeSpan CacheDuration { get; init; } = TimeSpan.FromHours(24);

    public NominatimOptions Nominatim { get; init; } = new();

    public GoogleGeocodingOptions Google { get; init; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Provider == GeocodingProvider.Nominatim && string.IsNullOrWhiteSpace(Nominatim.UserAgent))
        {
            yield return new ValidationResult("Nominatim requires an identifying User-Agent.", [nameof(Nominatim)]);
        }

        if (Provider == GeocodingProvider.Google && string.IsNullOrWhiteSpace(Google.ApiKey))
        {
            yield return new ValidationResult("Google geocoding requires Geocoding:Google:ApiKey (user secrets / env var).", [nameof(Google)]);
        }
    }
}

public sealed class NominatimOptions
{
    public Uri BaseUrl { get; init; } = new("https://nominatim.openstreetmap.org/");

    /// <summary>Identifies the application, as required by the Nominatim usage policy.</summary>
    public string UserAgent { get; init; } = string.Empty;

    public TimeSpan MinDelayBetweenRequests { get; init; } = TimeSpan.FromSeconds(1);
}

public sealed class GoogleGeocodingOptions
{
    public Uri BaseUrl { get; init; } = new("https://maps.googleapis.com/");

    public string? ApiKey { get; init; }
}
