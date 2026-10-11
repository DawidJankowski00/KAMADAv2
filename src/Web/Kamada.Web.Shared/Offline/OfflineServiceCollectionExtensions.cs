using Microsoft.Extensions.DependencyInjection;

namespace Kamada.Web.Shared.Offline;

public static class OfflineServiceCollectionExtensions
{
    /// <summary>
    /// Data kept on the device for working without a connection (the phone app). Needs <see cref="ServiceCollectionExtensions.AddKamadaClient"/>.
    /// </summary>
    public static IServiceCollection AddKamadaOffline(this IServiceCollection services)
    {
        services.AddSingleton<ILocalStore, IndexedDbLocalStore>();
        services.AddSingleton<OfflineLookup>();
        services.AddSingleton<OrderOutbox>();
        return services;
    }
}
