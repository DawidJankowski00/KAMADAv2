using Kamada.Modules.Catalog.Contracts;
using Kamada.Modules.Customers.Contracts;
using Kamada.Shared.Infrastructure.Geocoding;
using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Assistant.UnitTests;

/// <summary>In-memory customer directory: candidates and address matches are set up per test.</summary>
internal sealed class FakeCustomerDirectory : ICustomerDirectory
{
    public List<ClientCandidate> Candidates { get; } = [];

    public Dictionary<Guid, Guid> MatchingAddresses { get; } = [];

    public HashSet<string> TakenUsernames { get; } = [];

    public ClientMention? LastMention { get; private set; }

    public Task<IReadOnlyList<ClientCandidate>> FindClientCandidatesAsync(ClientMention mention, int limit, CancellationToken cancellationToken)
    {
        LastMention = mention;
        return Task.FromResult<IReadOnlyList<ClientCandidate>>([.. Candidates.OrderByDescending(c => c.Score).Take(limit)]);
    }

    public Task<ShippingAddressMatch?> FindShippingAddressAsync(Guid clientId, AddressMention mention, CancellationToken cancellationToken) =>
        Task.FromResult(MatchingAddresses.TryGetValue(clientId, out var addressId)
            ? new ShippingAddressMatch(addressId, mention.Street!, mention.BuildingNumber!, mention.ApartmentNumber, mention.City ?? string.Empty, "00-000", null, null)
            : null);

    public Task<string> SuggestUsernameAsync(string? name, string? surname, CancellationToken cancellationToken)
    {
        var baseName = $"{name}.{surname}".Trim('.').ToLowerInvariant();
        return Task.FromResult(TakenUsernames.Contains(baseName) ? baseName + "2" : baseName);
    }

    public Task<Result<ShippingDetails>> GetShippingDetailsAsync(Guid clientId, Guid addressId, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<IReadOnlyDictionary<Guid, AddressLocation>> GetAddressLocationsAsync(IReadOnlyCollection<Guid> addressIds, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}

/// <summary>Returns the candidates registered for a phrase (exact text).</summary>
internal sealed class FakeCatalogLookup : ICatalogLookup
{
    public Dictionary<string, List<ProductCandidate>> Candidates { get; } = new(StringComparer.Ordinal);

    public Task<IReadOnlyList<ProductCandidate>> FindProductCandidatesAsync(string phrase, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProductCandidate>>(Candidates.TryGetValue(phrase, out var found) ? [.. found.Take(limit)] : []);
}

internal sealed class FakeGeocoder(GeocodingResult? result) : IGeocodingService
{
    public List<GeocodingRequest> Requests { get; } = [];

    public Task<GeocodingResult?> GeocodeAsync(GeocodingRequest request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(result);
    }
}
