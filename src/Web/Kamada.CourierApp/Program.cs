using Kamada.CourierApp;
using Kamada.Shared.Contracts.Auth;
using Kamada.Web.Shared;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddKamadaClient(new KamadaClientOptions
{
    ApiBaseUrl = new Uri(apiBaseUrl.EndsWith('/') ? apiBaseUrl : apiBaseUrl + "/"),
    AllowedRoles = [Roles.Courier],
    StorageKey = "kamada.courier.session",
    SnackbarPosition = MudBlazor.Defaults.Classes.Position.TopCenter,
});

await builder.Build().RunAsync();
