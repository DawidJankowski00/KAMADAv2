using Kamada.Shared.Kernel.Domain;

namespace Kamada.Modules.Ordering.Domain.Orders;

/// <summary>
/// Order line. Product name, SKU and price are copied from the catalog when the order is placed.
/// </summary>
internal sealed class OrderItem : Entity
{
    private OrderItem(Guid id, Guid orderId, Guid productId, string productName, string sku, int quantity, decimal unitPrice)
        : base(id)
    {
        OrderId = orderId;
        ProductId = productId;
        ProductName = productName;
        Sku = sku;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    public string ProductName { get; private set; }

    public string Sku { get; private set; }

    public int Quantity { get; private set; }

    public decimal UnitPrice { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;

    internal static OrderItem Create(Guid orderId, OrderLine line) =>
        new(Guid.CreateVersion7(), orderId, line.ProductId, line.ProductName, line.Sku, line.Quantity, line.UnitPrice);
}

/// <summary>Input for placing an order: a product as reserved in the catalog.</summary>
internal sealed record OrderLine(Guid ProductId, string ProductName, string Sku, int Quantity, decimal UnitPrice);
