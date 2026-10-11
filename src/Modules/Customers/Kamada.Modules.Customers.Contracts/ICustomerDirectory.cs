using Kamada.Shared.Kernel.Results;
using Kamada.Shared.Matching.Addresses;
using Kamada.Shared.Matching.Clients;

namespace Kamada.Modules.Customers.Contracts;

/// <summary>Who and where to deliver to – copied into the order as a snapshot.</summary>
public sealed record ShippingDetails(
    Guid ClientId,
    string ClientName,
    string PhoneNumber,
    Guid AddressId,
    string Street,
    string BuildingNumber,
    string? ApartmentNumber,
    string City,
    string PostalCode,
    string Country,
    double? Latitude,
    double? Longitude);

/// <summary>Current coordinates of an address (may have been pinned after the order was placed).</summary>
public sealed record AddressLocation(Guid AddressId, double? Latitude, double? Longitude);

public interface ICustomerDirectory
{
    /// <summary>Fails when the client is unknown or inactive, or the address is not an active shipping address of that client.</summary>
    Task<Result<ShippingDetails>> GetShippingDetailsAsync(Guid clientId, Guid addressId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, AddressLocation>> GetAddressLocationsAsync(IReadOnlyCollection<Guid> addressIds, CancellationToken cancellationToken);

    /// <summary>
    /// Active clients similar to the mention, best first: exact phone number (digits only) or e-mail, then name + surname
    /// (diacritics folded, fuzzy). Returns at most <paramref name="limit"/> candidates; empty when nothing is similar.
    /// </summary>
    Task<IReadOnlyList<ClientCandidate>> FindClientCandidatesAsync(ClientMention mention, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// The client's active shipping address with the same street, building and apartment (folded, fuzzy street name);
    /// <c>null</c> when there is none or the match is not unique.
    /// </summary>
    Task<ShippingAddressMatch?> FindShippingAddressAsync(Guid clientId, AddressMention mention, CancellationToken cancellationToken);

    /// <summary>A free username for a new client: <c>name.surname</c> in ASCII, with a numeric suffix when taken.</summary>
    Task<string> SuggestUsernameAsync(string? name, string? surname, CancellationToken cancellationToken);
}
