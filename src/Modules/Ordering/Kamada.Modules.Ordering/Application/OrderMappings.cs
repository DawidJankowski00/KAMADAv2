using Kamada.Modules.Ordering.Domain.Orders;
using Kamada.Shared.Contracts.Ordering;
using ContractStatus = Kamada.Shared.Contracts.Ordering.OrderStatus;
using DomainStatus = Kamada.Modules.Ordering.Domain.Orders.OrderStatus;

namespace Kamada.Modules.Ordering.Application;

internal static class OrderMappings
{
    /// <param name="workerNames">Names from <c>IWorkerDirectory</c>; a worker missing there is shown by id only.</param>
    public static OrderDto ToDto(this Order order, IReadOnlyDictionary<Guid, string> workerNames) => new(
        order.Id,
        order.Number,
        order.ClientId,
        PlacedBy(order.PlacedByWorkerId, workerNames),
        order.Status.ToContract(),
        order.TotalAmount,
        order.Notes,
        order.ShippingAddress.ToDto(),
        [.. order.Items.Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.Sku, i.Quantity, i.UnitPrice, i.LineTotal))],
        order.CreatedAt,
        order.UpdatedAt);

    public static PlacedByDto? PlacedBy(Guid? workerId, IReadOnlyDictionary<Guid, string> workerNames) =>
        workerId is { } id ? new PlacedByDto(id, workerNames.GetValueOrDefault(id, string.Empty)) : null;

    public static ShippingAddressDto ToDto(this ShippingAddress address) => new(
        address.AddressId,
        address.RecipientName,
        address.PhoneNumber,
        address.Street,
        address.BuildingNumber,
        address.ApartmentNumber,
        address.City,
        address.PostalCode,
        address.Country,
        address.Latitude,
        address.Longitude);

    // Enum names are identical on both sides; an explicit parse keeps the domain enum internal.
    public static ContractStatus ToContract(this DomainStatus status) => Enum.Parse<ContractStatus>(status.ToString());

    public static DomainStatus ToDomain(this ContractStatus status) => Enum.Parse<DomainStatus>(status.ToString());
}
