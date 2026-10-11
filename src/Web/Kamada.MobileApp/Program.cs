using Kamada.MobileApp;
using Kamada.Shared.Contracts.Auth;
using Kamada.Web.Shared;
using Kamada.Web.Shared.Offline;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddKamadaClient(new KamadaClientOptions
{
    ApiBaseUrl = new Uri(apiBaseUrl.EndsWith('/') ? apiBaseUrl : apiBaseUrl + "/"),
    // Order = default role for a worker with several roles who has not picked one yet (most common daily work first).
    AllowedRoles = [Roles.Seller, Roles.Courier, Roles.Admin],
    StorageKey = "kamada.mobile.session",
    PreviousStorageKey = "kamada.courier.session",
    SnackbarPosition = MudBlazor.Defaults.Classes.Position.TopCenter,
    AssistantModelsUrl = builder.Configuration["AssistantModelsUrl"] ?? "models/",
});
builder.Services.AddKamadaOffline();

await builder.Build().RunAsync();
