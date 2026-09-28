using Kamada.AdminPanel;
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
    RequiredRole = Roles.Admin,
    StorageKey = "kamada.admin.session",
});

await builder.Build().RunAsync();
