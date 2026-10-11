namespace Kamada.Shared.Matching.Drafts;

public sealed record ExtractedAddress(string? Street, string? BuildingNumber, string? ApartmentNumber, string? City, string? PostalCode)
{
    public static ExtractedAddress None { get; } = new(null, null, null, null, null);

    public bool IsEmpty => Street is null && City is null;
}
