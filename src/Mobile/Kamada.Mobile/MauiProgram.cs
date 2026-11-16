using System.Reflection;
using Kamada.Mobile.Platform;
using Kamada.Mobile.Storage;
using Kamada.Web.Mobile;
using Kamada.Web.Shared;
using Kamada.Web.Shared.Offline;
using Kamada.Web.Shared.Platform;

namespace Kamada.Mobile;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder.UseMauiApp<App>();
        builder.Services.AddMauiBlazorWebView();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
#endif

        // Native platform services first: AddKamadaClient / AddKamadaOffline add the browser ones only when none is
        // registered. The browser ones cannot work here anyway: as singletons they would hold the root IJSRuntime,
        // which has no WebView.
        builder.Services.AddSingleton(SecureStorage.Default);
        builder.Services.AddSingleton(Connectivity.Current);
        builder.Services.AddSingleton<ISessionStorage, SecureSessionStorage>();
        builder.Services.AddSingleton<ConnectivityNetworkEvents>();
        builder.Services.AddSingleton<INetworkEvents>(sp => sp.GetRequiredService<ConnectivityNetworkEvents>());
        builder.Services.AddSingleton<ILocalStore>(_ => new SqliteLocalStore(Path.Combine(FileSystem.AppDataDirectory, "kamada-local.db")));

        builder.Services.AddKamadaClient(MobileClient.Options(ApiBaseUrl()));
        builder.Services.AddKamadaOffline();

        return builder.Build();
    }

    /// <summary>Set at build time (<c>KamadaApiBaseUrl</c> in the project file).</summary>
    private static Uri ApiBaseUrl()
    {
        var url = typeof(MauiProgram).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == "KamadaApiBaseUrl").Value!;
        return new Uri(url.EndsWith('/') ? url : url + "/");
    }
}
