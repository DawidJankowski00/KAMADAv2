using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Catalog.Contracts;

public sealed record StockLine(Guid ProductId, int Quantity);

/// <summary>Product data captured at reservation time (price is frozen into the order).</summary>
public sealed record ReservedProduct(Guid ProductId, string Name, string Sku, decimal UnitPrice, int Quantity);

/// <summary>
/// The only way other modules can change stock. Call inside <c>IUnitOfWork</c> so the stock change commits
/// or rolls back together with the caller's own changes.
/// </summary>
public interface ICatalogInventory
{
    /// <summary>
    /// Reserves all lines or none. Fails when a product does not exist, is inactive or has too little stock.
    /// </summary>
    Task<Result<IReadOnlyList<ReservedProduct>>> ReserveAsync(IReadOnlyList<StockLine> lines, CancellationToken cancellationToken);

    /// <summary>Returns previously reserved units to stock (order cancellation).</summary>
    Task ReleaseAsync(IReadOnlyList<StockLine> lines, CancellationToken cancellationToken);
}
