using System.Reflection;
using Kamada.Web.Mobile;
using Kamada.Web.Shared;
using Kamada.Web.Shared.Offline;

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
