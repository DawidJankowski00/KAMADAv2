namespace Kamada.Shared.Contracts.Ordering;

public enum OrderStatus
{
    New,
    Paid,
    ReadyForDelivery,
    AssignedToRun,
    OutForDelivery,
    Delivered,
    DeliveryFailed,
    Cancelled,
}

public sealed record OrderLineRequest(Guid ProductId, int Quantity);

public sealed record PlaceOrderRequest(Guid ClientId, Guid ShippingAddressId, IReadOnlyList<OrderLineRequest> Items, string? Notes = null);

public sealed record ShippingAddressDto(
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

public sealed record OrderItemDto(Guid ProductId, string ProductName, string Sku, int Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record OrderDto(
    Guid Id,
    long Number,
    Guid ClientId,
    OrderStatus Status,
    decimal TotalAmount,
    string? Notes,
    ShippingAddressDto ShippingAddress,
    IReadOnlyList<OrderItemDto> Items,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record OrderSummaryDto(
    Guid Id,
    long Number,
    Guid ClientId,
    string RecipientName,
    string City,
    OrderStatus Status,
    decimal TotalAmount,
    int ItemCount,
    DateTimeOffset CreatedAt);

/// <param name="Search">Order number or recipient name.</param>
/// <param name="From">Created at or after (inclusive).</param>
/// <param name="To">Created before (exclusive).</param>
public sealed record OrderListQuery(
    OrderStatus? Status = null,
    Guid? ClientId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    string? Search = null,
    int Page = 1,
    int PageSize = Common.Paging.DefaultPageSize);

public sealed record OrderStatusCountDto(OrderStatus Status, int Count);
