using Kamada.Shared.Matching.Addresses;
using Kamada.Shared.Matching.Clients;
using Kamada.Shared.Matching.Drafts;
using Kamada.Shared.Matching.Products;

namespace Kamada.Shared.Matching.UnitTests;

/// <summary>In-memory draft data: client candidates, address matches, product candidates and the geocoder are set up per test.</summary>
internal sealed class FakeDraftSource : IOrderDraftSource
{
    public List<ClientCandidate> Candidates { get; } = [];

    public Dictionary<Guid, Guid> MatchingAddresses { get; } = [];

    public HashSet<string> TakenUsernames { get; } = [];

    /// <summary>Product candidates registered for a phrase (exact text).</summary>
    public Dictionary<string, List<ProductCandidate>> Products { get; } = new(StringComparer.Ordinal);

    public GeocodedPlace? Place { get; set; }

    public ClientMention? LastMention { get; private set; }

    public List<AddressMention> GeocodedAddresses { get; } = [];

    public Task<IReadOnlyList<ClientCandidate>> FindClientCandidatesAsync(ClientMention mention, int limit, CancellationToken cancellationToken)
    {
        LastMention = mention;
        return Task.FromResult<IReadOnlyList<ClientCandidate>>([.. Candidates.OrderByDescending(c => c.Score).Take(limit)]);
    }

    public Task<Guid?> FindShippingAddressAsync(Guid clientId, AddressMention mention, CancellationToken cancellationToken) =>
        Task.FromResult(MatchingAddresses.TryGetValue(clientId, out var addressId) ? (Guid?)addressId : null);

    public Task<string> SuggestUsernameAsync(string? name, string? surname, CancellationToken cancellationToken)
    {
        var baseName = $"{name}.{surname}".Trim('.').ToLowerInvariant();
        return Task.FromResult(TakenUsernames.Contains(baseName) ? baseName + "2" : baseName);
    }

    public Task<IReadOnlyList<ProductCandidate>> FindProductCandidatesAsync(string phrase, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ProductCandidate>>(Products.TryGetValue(phrase, out var found) ? [.. found.Take(limit)] : []);

    public Task<GeocodedPlace?> GeocodeAsync(AddressMention address, string country, CancellationToken cancellationToken)
    {
        GeocodedAddresses.Add(address);
        return Task.FromResult(Place);
    }
}
