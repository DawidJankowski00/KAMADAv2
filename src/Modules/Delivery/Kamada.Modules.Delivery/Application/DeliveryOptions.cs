using System.ComponentModel.DataAnnotations;

namespace Kamada.Modules.Delivery.Application;

internal sealed class DeliveryOptions : IValidatableObject
{
    public const string SectionName = "Delivery";

    /// <summary>Time spent at each stop (parking, handing over) added to arrival estimates of later stops.</summary>
    public TimeSpan ServiceTimePerStop { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>IANA time zone that defines "today" for couriers' run lists.</summary>
    [Required]
    public string TimeZone { get; set; } = "Europe/Warsaw";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ServiceTimePerStop < TimeSpan.Zero || ServiceTimePerStop > TimeSpan.FromHours(1))
        {
            yield return new ValidationResult("ServiceTimePerStop must be between 0 and 1 hour.", [nameof(ServiceTimePerStop)]);
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(TimeZone, out _))
        {
            yield return new ValidationResult($"Unknown time zone '{TimeZone}'.", [nameof(TimeZone)]);
        }
    }
}
