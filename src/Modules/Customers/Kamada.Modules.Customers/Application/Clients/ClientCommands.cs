using Kamada.Modules.Customers.Application.Abstractions;
using Kamada.Modules.Customers.Domain.Clients;
using Kamada.Shared.Contracts.Customers;
using Kamada.Shared.Infrastructure.Persistence;
using Kamada.Shared.Kernel.Geography;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Customers.Application.Clients;

internal sealed class ClientCommands(ICustomersDbContext db, AddressGeocoder geocoder, TimeProvider clock)
{
    public async Task<Result<ClientDto>> CreateAsync(CreateClientRequest request, CancellationToken cancellationToken)
    {
        // A retry of a create that already succeeded (checked first: the username is "taken" by that very client).
        if (request.RequestId is { } requestId && await FindByRequestIdAsync(requestId, cancellationToken) is { } created)
        {
            return created.ToDto();
        }

        var uniqueness = await CheckUniqueAsync(null, request.Username, request.Email, cancellationToken);
        if (uniqueness.IsFailure)
        {
            // An identical retry may have been saved between the check above and this one: then the "taken" username is its own.
            if (request.RequestId is { } sameRequest && await FindByRequestIdAsync(sameRequest, cancellationToken) is { } savedMeanwhile)
            {
                return savedMeanwhile.ToDto();
            }

            return uniqueness.Error;
        }

        var client = Client.Create(request.Username, request.Email, request.Name, request.Surname, request.PhoneNumber, request.RequestId);
        foreach (var addressRequest in request.Addresses ?? [])
        {
            var address = client.AddAddress(addressRequest.ToDomain());
            await geocoder.TryGeocodeAsync(address, cancellationToken);
        }

        db.Clients.Add(client);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (request.RequestId is not null && ex.IsUniqueViolation())
        {
            // An identical retry raced this one and was saved first; otherwise it is a real conflict.
            var savedFirst = await FindByRequestIdAsync(request.RequestId.Value, cancellationToken);
            if (savedFirst is null)
            {
                throw;
            }

            return savedFirst.ToDto();
        }

        return client.ToDto();
    }

    public async Task<Result<ClientDto>> UpdateAsync(Guid id, UpdateClientRequest request, CancellationToken cancellationToken)
    {
        var client = await LoadAsync(id, cancellationToken);
        if (client is null)
        {
            return ClientErrors.NotFound;
        }

        var uniqueness = await CheckUniqueAsync(id, client.Username, request.Email, cancellationToken);
        if (uniqueness.IsFailure)
        {
            return uniqueness.Error;
        }

        client.UpdateDetails(request.Email, request.Name, request.Surname, request.PhoneNumber);
        await db.SaveChangesAsync(cancellationToken);
        return client.ToDto();
    }

    public Task<Result> DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        ModifyAsync(id, client =>
        {
            client.Deactivate();
            return Result.Success();
        }, cancellationToken);

    public Task<Result> ActivateAsync(Guid id, CancellationToken cancellationToken) =>
        ModifyAsync(id, client =>
        {
            client.Activate();
            return Result.Success();
        }, cancellationToken);

    public async Task<Result<AddressDto>> AddAddressAsync(Guid clientId, AddressRequest request, CancellationToken cancellationToken)
    {
        if (request.RequestId is { } requestId && await FindAddressByRequestIdAsync(requestId, cancellationToken) is { } created)
        {
            return created.ClientId == clientId ? created.ToDto() : RequestErrors.IdReused;
        }

        var client = await LoadAsync(clientId, cancellationToken);
        if (client is null)
        {
            return ClientErrors.NotFound;
        }

        var address = client.AddAddress(request.ToDomain(), request.RequestId);
        await geocoder.TryGeocodeAsync(address, cancellationToken);

        // Adding to a tracked collection: tell EF the address is new (its key is client-generated).
        db.Addresses.Add(address);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (request.RequestId is not null && ex.IsUniqueViolation())
        {
            var savedFirst = await FindAddressByRequestIdAsync(request.RequestId.Value, cancellationToken);
            if (savedFirst is null)
            {
                throw;
            }

            return savedFirst.ClientId == clientId ? savedFirst.ToDto() : RequestErrors.IdReused;
        }

        return address.ToDto();
    }

    public async Task<Result<AddressDto>> UpdateAddressAsync(
        Guid clientId,
        Guid addressId,
        AddressRequest request,
        CancellationToken cancellationToken)
    {
        var client = await LoadAsync(clientId, cancellationToken);
        if (client is null)
        {
            return ClientErrors.NotFound;
        }

        var updated = client.UpdateAddress(addressId, request.ToDomain(), out var locationReset);
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        if (locationReset)
        {
            await geocoder.TryGeocodeAsync(updated.Value, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return updated.Value.ToDto();
    }

    public Task<Result> ArchiveAddressAsync(Guid clientId, Guid addressId, CancellationToken cancellationToken) =>
        ModifyAsync(clientId, client => client.ArchiveAddress(addressId), cancellationToken);

    /// <summary>Pin placed by the admin on the map, e.g. when geocoding could not find the address.</summary>
    public async Task<Result<AddressDto>> SetAddressLocationAsync(
        Guid clientId,
        Guid addressId,
        SetAddressLocationRequest request,
        CancellationToken cancellationToken)
    {
        var address = await FindAddressAsync(clientId, addressId, cancellationToken);
        if (address.IsFailure)
        {
            return address.Error;
        }

        address.Value.SetManualLocation(new GeoPoint(request.Latitude, request.Longitude), clock.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return address.Value.ToDto();
    }

    /// <summary>Retries automatic geocoding (e.g. after the provider was unavailable).</summary>
    public async Task<Result<AddressDto>> GeocodeAddressAsync(Guid clientId, Guid addressId, CancellationToken cancellationToken)
    {
        var address = await FindAddressAsync(clientId, addressId, cancellationToken);
        if (address.IsFailure)
        {
            return address.Error;
        }

        if (!await geocoder.TryGeocodeAsync(address.Value, cancellationToken))
        {
            return Error.Conflict("Clients.GeocodingFailed", "The address could not be geocoded. Place the pin on the map manually.");
        }

        await db.SaveChangesAsync(cancellationToken);
        return address.Value.ToDto();
    }

    private async Task<Result<Address>> FindAddressAsync(Guid clientId, Guid addressId, CancellationToken cancellationToken)
    {
        var client = await LoadAsync(clientId, cancellationToken);
        return client is null ? ClientErrors.NotFound : client.FindActiveAddress(addressId);
    }

    private Task<Client?> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        db.Clients.Include(c => c.Addresses).SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    // Not tracked: after a failed save the context still tracks the rejected entity.
    private Task<Client?> FindByRequestIdAsync(Guid requestId, CancellationToken cancellationToken) =>
        db.Clients.AsNoTracking().Include(c => c.Addresses).SingleOrDefaultAsync(c => c.RequestId == requestId, cancellationToken);

    private Task<Address?> FindAddressByRequestIdAsync(Guid requestId, CancellationToken cancellationToken) =>
        db.Addresses.AsNoTracking().SingleOrDefaultAsync(a => a.RequestId == requestId, cancellationToken);

    private async Task<Result> ModifyAsync(Guid id, Func<Client, Result> change, CancellationToken cancellationToken)
    {
        var client = await LoadAsync(id, cancellationToken);
        if (client is null)
        {
            return ClientErrors.NotFound;
        }

        var result = change(client);
        if (result.IsSuccess)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    private async Task<Result> CheckUniqueAsync(Guid? clientId, string username, string email, CancellationToken cancellationToken)
    {
        var normalizedUsername = Client.NormalizeUsername(username);
        if (await db.Clients.AnyAsync(c => c.Username == normalizedUsername && c.Id != clientId, cancellationToken))
        {
            return ClientErrors.UsernameTaken;
        }

        var normalizedEmail = Client.NormalizeEmail(email);
        return await db.Clients.AnyAsync(c => c.Email == normalizedEmail && c.Id != clientId, cancellationToken)
            ? ClientErrors.EmailTaken
            : Result.Success();
    }
}
