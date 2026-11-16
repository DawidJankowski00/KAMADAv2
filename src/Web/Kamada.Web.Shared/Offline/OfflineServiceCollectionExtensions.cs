using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kamada.Web.Shared.Offline;

public static class OfflineServiceCollectionExtensions
{
    /// <summary>
    /// Data kept on the device for working without a connection (the phone app): the local store, the local copy of
    /// clients and products and the order outbox. Needs <see cref="ServiceCollectionExtensions.AddKamadaClient"/>.
    /// </summary>
    public static IServiceCollection AddKamadaOffline(this IServiceCollection services)
    {
        services.TryAddSingleton<ILocalStore, IndexedDbLocalStore>(); // a native host registers its own first
        services.AddSingleton<OfflineLookup>();
        services.AddSingleton<OrderOutbox>();
        return services;
    }
}
