using Kamada.Shared.Contracts.Catalog;
using Kamada.Shared.Contracts.Customers;
using Kamada.Shared.Matching.Addresses;
using Kamada.Shared.Matching.Clients;
using Kamada.Shared.Matching.Drafts;
using Kamada.Shared.Matching.Products;

namespace Kamada.Web.Shared.Assistant;

/// <summary>
/// The phone's copy of clients and products (<c>OfflineLookup</c>) as the data of a draft, ranked by the very same
/// matchers as on the server. Nothing can be geocoded without a connection, so new addresses keep what was said.
/// </summary>
public sealed class LocalDraftSource(IReadOnlyList<ClientLookupDto> clients, IReadOnlyList<ProductLookupDto> products) : IOrderDraftSource
{
    public Task<IReadOnlyList<ClientCandidate>> FindClientCandidatesAsync(ClientMention mention, int limit, CancellationToken cancellationToken)
    {
        var rows = clients.Select(c => new ClientRow(c.Id, c.Name, c.Surname, c.Email, c.PhoneNumber));
        return Task.FromResult(ClientMatcher.Rank(mention, rows, limit));
    }

    public Task<Guid?> FindShippingAddressAsync(Guid clientId, AddressMention mention, CancellationToken cancellationToken)
    {
        var client = clients.FirstOrDefault(c => c.Id == clientId);
        var addresses = (client?.ShippingAddresses ?? [])
            .Where(a => a.Type == AddressType.Shipping)
            .Select(a => new AddressRow(a.Id, a.Street, a.BuildingNumber, a.ApartmentNumber, a.City, a.PostalCode, a.Latitude, a.Longitude))
            .ToList();
        return Task.FromResult<Guid?>(AddressMatcher.FindSame(mention, addresses)?.Id);
    }

    /// <remarks>Only the usernames on the phone are known: a taken one is refused by the server when the order is sent.</remarks>
    public Task<string> SuggestUsernameAsync(string? name, string? surname, CancellationToken cancellationToken)
    {
        var taken = clients.Select(c => c.Username).ToHashSet(StringComparer.Ordinal);
        return Task.FromResult(UsernameSuggestion.FirstFree(UsernameSuggestion.Base(name, surname), taken));
    }

    public Task<IReadOnlyList<ProductCandidate>> FindProductCandidatesAsync(string phrase, int limit, CancellationToken cancellationToken)
    {
        var rows = products.Select(p => new ProductRow(p.Id, p.Name, p.Sku, p.Price, p.StockQuantity));
        return Task.FromResult(ProductMatcher.Rank(phrase, rows, limit));
    }

    public Task<GeocodedPlace?> GeocodeAsync(AddressMention address, string country, CancellationToken cancellationToken) =>
        Task.FromResult<GeocodedPlace?>(null);
}
