using Kamada.Web.Shared.Assistant;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.Web.Shared.Offline;

public static class OfflineServiceCollectionExtensions
{
    /// <summary>
    /// Data and models kept on the device for working without a connection (the phone app): the local store, the order
    /// outbox and the on-device assistant, which takes over from the server's one when the connection is poor.
    /// Needs <see cref="ServiceCollectionExtensions.AddKamadaClient"/>.
    /// </summary>
    public static IServiceCollection AddKamadaOffline(this IServiceCollection services)
    {
        services.AddSingleton<ILocalStore, IndexedDbLocalStore>();
        services.AddSingleton<OfflineLookup>();
        services.AddSingleton<OrderOutbox>();

        services.AddSingleton<LocalAssistantRuntime>();
        services.AddSingleton<LocalAssistantSettings>();
        services.AddSingleton<LocalOrderAssistant>();
        services.AddTransient<ServerOrderAssistant>();
        services.AddSingleton<RoutingOrderAssistant>();
        services.AddSingleton<IOrderAssistant>(sp => sp.GetRequiredService<RoutingOrderAssistant>());
        return services;
    }
}
