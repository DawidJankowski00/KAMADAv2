using Kamada.Modules.Customers.Application.Abstractions;
using Kamada.Modules.Customers.Application.Lookup;
using Kamada.Modules.Customers.Contracts;
using Kamada.Modules.Customers.Domain.Clients;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Customers.Application;

/// <summary>Public read API of the Customers module.</summary>
internal sealed class CustomerDirectory(ICustomersDbContext db) : ICustomerDirectory
{
    public async Task<Result<ShippingDetails>> GetShippingDetailsAsync(Guid clientId, Guid addressId, CancellationToken cancellationToken)
    {
        var client = await db.Clients.AsNoTracking()
            .Include(c => c.Addresses)
            .SingleOrDefaultAsync(c => c.Id == clientId, cancellationToken);

        if (client is null)
        {
            return Error.Validation("Clients.NotFound", "Client does not exist.");
        }

        if (!client.IsActive)
        {
            return Error.Conflict("Clients.Inactive", "Client is deactivated.");
        }

        var address = client.ActiveAddresses.SingleOrDefault(a => a.Id == addressId);
        if (address is null)
        {
            return Error.Validation("Clients.AddressNotFound", "The address does not belong to this client or was archived.");
        }

        if (address.Type != AddressType.Shipping)
        {
            return Error.Validation("Clients.NotShippingAddress", "Choose a shipping address.");
        }

        return new ShippingDetails(
            client.Id,
            $"{client.Name} {client.Surname}",
            client.PhoneNumber,
            address.Id,
            address.Street,
            address.BuildingNumber,
            address.ApartmentNumber,
            address.City,
            address.PostalCode,
            address.Country,
            address.Latitude,
            address.Longitude);
    }

    public async Task<IReadOnlyDictionary<Guid, AddressLocation>> GetAddressLocationsAsync(
        IReadOnlyCollection<Guid> addressIds,
        CancellationToken cancellationToken)
    {
        var ids = addressIds.Distinct().ToList();
        return await db.Addresses.AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => new AddressLocation(a.Id, a.Latitude, a.Longitude), cancellationToken);
    }

    /// <remarks>
    /// Fuzzy matching runs in memory over a narrow projection of active clients – fine for a shop with thousands of
    /// clients; a larger one would pre-filter in SQL (e.g. <c>pg_trgm</c> on folded names).
    /// </remarks>
    public async Task<IReadOnlyList<ClientCandidate>> FindClientCandidatesAsync(
        ClientMention mention,
        int limit,
        CancellationToken cancellationToken)
    {
        if (mention.IsEmpty || limit <= 0)
        {
            return [];
        }

        var clients = await db.Clients.AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new ClientRow(c.Id, c.Name, c.Surname, c.Email, c.PhoneNumber))
            .ToListAsync(cancellationToken);

        return ClientMatcher.Rank(mention, clients, limit);
    }

    public async Task<ShippingAddressMatch?> FindShippingAddressAsync(
        Guid clientId,
        AddressMention mention,
        CancellationToken cancellationToken)
    {
        if (mention.IsEmpty)
        {
            return null;
        }

        var addresses = await db.Addresses.AsNoTracking()
            .Where(a => a.ClientId == clientId && !a.IsArchived && a.Type == AddressType.Shipping)
            .Select(a => new AddressRow(a.Id, a.Street, a.BuildingNumber, a.ApartmentNumber, a.City, a.PostalCode, a.Latitude, a.Longitude))
            .ToListAsync(cancellationToken);

        var match = AddressMatcher.FindSame(mention, addresses);
        return match is null
            ? null
            : new ShippingAddressMatch(
                match.Id, match.Street, match.BuildingNumber, match.ApartmentNumber, match.City, match.PostalCode, match.Latitude, match.Longitude);
    }

    public async Task<string> SuggestUsernameAsync(string? name, string? surname, CancellationToken cancellationToken)
    {
        var baseName = UsernameSuggestion.Base(name, surname);
        var taken = await db.Clients.AsNoTracking()
            .Where(c => c.Username.StartsWith(baseName))
            .Select(c => c.Username)
            .ToListAsync(cancellationToken);

        return UsernameSuggestion.FirstFree(baseName, taken.ToHashSet(StringComparer.Ordinal));
    }
}
