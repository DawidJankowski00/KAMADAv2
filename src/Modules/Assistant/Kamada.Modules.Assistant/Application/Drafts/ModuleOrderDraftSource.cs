using Kamada.Modules.Catalog.Contracts;
using Kamada.Modules.Customers.Contracts;
using Kamada.Shared.Infrastructure.Geocoding;
using Kamada.Shared.Matching.Addresses;
using Kamada.Shared.Matching.Clients;
using Kamada.Shared.Matching.Drafts;
using Kamada.Shared.Matching.Products;

namespace Kamada.Modules.Assistant.Application.Drafts;

/// <summary>The server's draft data: clients and products of the other modules (through their contracts) and the geocoder.</summary>
internal sealed class ModuleOrderDraftSource(ICustomerDirectory customers, ICatalogLookup catalog, IGeocodingService geocoding) : IOrderDraftSource
{
    public Task<IReadOnlyList<ClientCandidate>> FindClientCandidatesAsync(ClientMention mention, int limit, CancellationToken cancellationToken) =>
        customers.FindClientCandidatesAsync(mention, limit, cancellationToken);

    public async Task<Guid?> FindShippingAddressAsync(Guid clientId, AddressMention mention, CancellationToken cancellationToken) =>
        (await customers.FindShippingAddressAsync(clientId, mention, cancellationToken))?.AddressId;

    public Task<string> SuggestUsernameAsync(string? name, string? surname, CancellationToken cancellationToken) =>
        customers.SuggestUsernameAsync(name, surname, cancellationToken);

    public Task<IReadOnlyList<ProductCandidate>> FindProductCandidatesAsync(string phrase, int limit, CancellationToken cancellationToken) =>
        catalog.FindProductCandidatesAsync(phrase, limit, cancellationToken);

    public async Task<GeocodedPlace?> GeocodeAsync(AddressMention address, string country, CancellationToken cancellationToken)
    {
        var result = await geocoding.GeocodeAsync(
            new GeocodingRequest(address.Street ?? string.Empty, address.BuildingNumber ?? string.Empty, address.City ?? string.Empty, address.PostalCode ?? string.Empty, country),
            cancellationToken);
        return result is null
            ? null
            : new GeocodedPlace(
                result.Location.Latitude, result.Location.Longitude,
                result.Address?.Street, result.Address?.BuildingNumber, result.Address?.PostalCode, result.Address?.City);
    }
}
