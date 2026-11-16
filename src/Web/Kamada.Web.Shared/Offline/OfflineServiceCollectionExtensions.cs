using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kamada.Web.Shared.Offline;

public static class OfflineServiceCollectionExtensions
{
    /// <summary>
    /// Data kept on the device for working without a connection (the phone app): the local store, the local copy of
    /// clients and products and the order outbox. Needs <see cref="ServiceCollectionExtensions.AddKamadaClient"/>
    /// and an <see cref="ILocalStore"/> registered by the host (the native app: SQLite).
    /// </summary>
    public static IServiceCollection AddKamadaOffline(this IServiceCollection services)
    {
        services.AddSingleton<OfflineLookup>();
        services.AddSingleton<OrderOutbox>();
        return services;
    }
}
