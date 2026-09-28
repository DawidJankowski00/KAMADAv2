using Kamada.Modules.Customers.Application.Abstractions;
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
}
