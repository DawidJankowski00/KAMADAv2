using Kamada.Shared.Kernel.Domain;
using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Customers.Domain.Clients;

/// <summary>
/// Shop customer (aggregate root) with their shipping and billing addresses.
/// Clients are deactivated rather than deleted, because orders reference them.
/// </summary>
internal sealed class Client : Entity, IAuditable
{
    private readonly List<Address> _addresses = [];

    private Client(Guid id, string username, string email, string name, string surname, string phoneNumber)
        : base(id)
    {
        Username = username;
        Email = email;
        Name = name;
        Surname = surname;
        PhoneNumber = phoneNumber;
        IsActive = true;
    }

    public string Username { get; private set; }

    public string Email { get; private set; }

    public string Name { get; private set; }

    public string Surname { get; private set; }

    public string PhoneNumber { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Id the creating request carried (idempotent retries), if any.</summary>
    public Guid? RequestId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public IReadOnlyCollection<Address> Addresses => _addresses;

    public IEnumerable<Address> ActiveAddresses => _addresses.Where(a => !a.IsArchived);

    public static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static Client Create(string username, string email, string name, string surname, string phoneNumber, Guid? requestId = null) =>
        new(Guid.CreateVersion7(), NormalizeUsername(username), NormalizeEmail(email), name.Trim(), surname.Trim(), phoneNumber.Trim())
        {
            RequestId = requestId,
        };

    public void UpdateDetails(string email, string name, string surname, string phoneNumber)
    {
        Email = NormalizeEmail(email);
        Name = name.Trim();
        Surname = surname.Trim();
        PhoneNumber = phoneNumber.Trim();
    }

    public Address AddAddress(AddressDetails details, Guid? requestId = null)
    {
        var address = Address.Create(Id, details, requestId);
        _addresses.Add(address);
        return address;
    }

    public Result<Address> FindActiveAddress(Guid addressId)
    {
        var address = _addresses.SingleOrDefault(a => a.Id == addressId);
        return address is null || address.IsArchived ? ClientErrors.AddressNotFound : address;
    }

    /// <summary>Updates an address; the out flag tells whether it needs geocoding again.</summary>
    public Result<Address> UpdateAddress(Guid addressId, AddressDetails details, out bool locationReset)
    {
        locationReset = false;
        var found = FindActiveAddress(addressId);
        if (found.IsFailure)
        {
            return found;
        }

        locationReset = found.Value.Update(details);
        return found;
    }

    public Result ArchiveAddress(Guid addressId)
    {
        var found = FindActiveAddress(addressId);
        if (found.IsFailure)
        {
            return found.Error;
        }

        found.Value.Archive();
        return Result.Success();
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
