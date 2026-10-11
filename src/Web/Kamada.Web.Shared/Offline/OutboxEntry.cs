using Kamada.Shared.Contracts.Customers;
using Kamada.Shared.Contracts.Ordering;

namespace Kamada.Web.Shared.Offline;

public enum OutboxStatus
{
    /// <summary>Waiting for a connection (or being sent right now).</summary>
    Pending,

    /// <summary>The server refused it (e.g. not enough stock): the worker edits or discards it.</summary>
    NeedsAttention,
}

/// <summary>
/// An order taken on the phone and not sent yet, possibly with a new client and/or a new shipping address. It is sent in
/// steps (client → address → order), each with its own <c>RequestId</c>, and the ids the server returns are kept in the
/// entry, so an interrupted send resumes where it stopped and a repeated step never creates a duplicate.
/// </summary>
/// <param name="Id">The order's <see cref="PlaceOrderRequest.RequestId"/>; also the key on the device.</param>
/// <param name="NewClient">Client to create first (its address included); null for an existing client.</param>
/// <param name="ClientId">The existing client, or the created one once the first step succeeded.</param>
/// <param name="NewAddress">Shipping address to add to an existing client; null when <see cref="AddressId"/> is known.</param>
/// <param name="AddressId">The shipping address, known up front or once it was created.</param>
/// <param name="Client">What the worker saw when taking the order (names, phone, addresses), for the list and editing.</param>
/// <param name="ShippingAddressLabel">The delivery address as text, for the list.</param>
/// <param name="Problem">Why the server refused the entry (Polish text), for <see cref="OutboxStatus.NeedsAttention"/>.</param>
public sealed record OutboxEntry(
    Guid Id,
    DateTimeOffset CreatedAt,
    OutboxStatus Status,
    CreateClientRequest? NewClient,
    Guid? ClientId,
    AddressRequest? NewAddress,
    Guid? AddressId,
    ClientLookupDto Client,
    string ShippingAddressLabel,
    IReadOnlyList<OutboxLine> Lines,
    string? Notes,
    string? Problem = null,
    string? ProblemCode = null,
    int Attempts = 0,
    DateTimeOffset? LastAttemptAt = null)
{
    /// <summary>Price × quantity from the device's copy of the catalogue; the server computes the real total.</summary>
    public decimal EstimatedTotal => Lines.Sum(l => l.UnitPrice * l.Quantity);

    public PlaceOrderRequest ToOrderRequest() => new(
        ClientId ?? throw new InvalidOperationException("The client is not created yet."),
        AddressId ?? throw new InvalidOperationException("The address is not created yet."),
        [.. Lines.Select(l => new OrderLineRequest(l.ProductId, l.Quantity))],
        Notes,
        Id);
}

/// <param name="UnitPrice">Price on the device when the order was taken.</param>
public sealed record OutboxLine(Guid ProductId, string Sku, string Name, int Quantity, decimal UnitPrice);
