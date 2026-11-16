using Kamada.Web.Mobile;
using Kamada.Web.Shared;
using Kamada.Web.Shared.Offline;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

// The PWA only hosts the phone app (Kamada.Web.Mobile) in the browser.
var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress;
builder.Services.AddKamadaClient(MobileClient.Options(new Uri(apiBaseUrl.EndsWith('/') ? apiBaseUrl : apiBaseUrl + "/")));
builder.Services.AddKamadaOffline();

await builder.Build().RunAsync();
