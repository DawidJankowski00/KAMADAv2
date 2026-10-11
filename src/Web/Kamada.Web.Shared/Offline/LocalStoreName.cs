namespace Kamada.Web.Shared.Offline;

public enum LocalStoreName
{
    /// <summary>Products and clients downloaded for ordering without a connection.</summary>
    Lookup,

    /// <summary>Orders (with new clients and addresses) waiting to be sent.</summary>
    Outbox,

    /// <summary>Bookkeeping, e.g. whose data the device holds.</summary>
    Meta,
}
