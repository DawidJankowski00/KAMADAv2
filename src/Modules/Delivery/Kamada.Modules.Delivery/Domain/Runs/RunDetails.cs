namespace Kamada.Modules.Delivery.Domain.Runs;

/// <summary>Editable header of a delivery run (value object, normalized on creation).</summary>
internal sealed record RunDetails
{
    public RunDetails(string name, DateOnly plannedDate, Guid courierId, string vehiclePlate, Guid startLocationId, Guid endLocationId)
    {
        Name = name.Trim();
        PlannedDate = plannedDate;
        CourierId = courierId;
        VehiclePlate = NormalizePlate(vehiclePlate);
        StartLocationId = startLocationId;
        EndLocationId = endLocationId;
    }

    public string Name { get; }

    public DateOnly PlannedDate { get; }

    public Guid CourierId { get; }

    public string VehiclePlate { get; }

    public Guid StartLocationId { get; }

    public Guid EndLocationId { get; }

    /// <summary>"cb 12345" → "CB 12345" (single spaces, upper case).</summary>
    public static string NormalizePlate(string plate) =>
        string.Join(' ', plate.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToUpperInvariant();
}
