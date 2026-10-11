using Kamada.Shared.Matching.Addresses;
using Kamada.Shared.Matching.Clients;
using Kamada.Shared.Matching.Products;

namespace Kamada.Shared.Matching.Drafts;

/// <summary>
/// The data an order draft is matched against: the server's modules, or the copy kept on the phone. Implementations
/// rank with <see cref="ClientMatcher"/>, <see cref="AddressMatcher"/> and <see cref="ProductMatcher"/>, so a draft is
/// the same wherever it is made.
/// </summary>
public interface IOrderDraftSource
{
    /// <summary>Active clients similar to the mention, best first, at most <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<ClientCandidate>> FindClientCandidatesAsync(ClientMention mention, int limit, CancellationToken cancellationToken);

    /// <summary>The client's active shipping address equal to the mentioned one; <c>null</c> when none (or not unique).</summary>
    Task<Guid?> FindShippingAddressAsync(Guid clientId, AddressMention mention, CancellationToken cancellationToken);

    /// <summary>A free username for a new client (<see cref="UsernameSuggestion"/>).</summary>
    Task<string> SuggestUsernameAsync(string? name, string? surname, CancellationToken cancellationToken);

    /// <summary>Active products similar to the phrase, best first, at most <paramref name="limit"/>.</summary>
    Task<IReadOnlyList<ProductCandidate>> FindProductCandidatesAsync(string phrase, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Looks the spoken address up (postal code, coordinates); <c>null</c> when it was not found or cannot be looked up
    /// (e.g. on the phone without a connection).
    /// </summary>
    Task<GeocodedPlace?> GeocodeAsync(AddressMention address, string country, CancellationToken cancellationToken);
}
