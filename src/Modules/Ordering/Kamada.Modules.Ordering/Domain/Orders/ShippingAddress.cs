namespace Kamada.Modules.Ordering.Domain.Orders;

/// <summary>
/// Snapshot of the recipient and address at the moment the order was placed, so later edits of the
/// client's address book do not rewrite order history.
/// </summary>
internal sealed record ShippingAddress(
    Guid AddressId,
    string RecipientName,
    string PhoneNumber,
    string Street,
    string BuildingNumber,
    string? ApartmentNumber,
    string City,
    string PostalCode,
    string Country,
    double? Latitude,
    double? Longitude);
