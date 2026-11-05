using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using ADSync.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

System.Globalization.CultureInfo.DefaultThreadCurrentCulture = new("pt-BR");
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = new("pt-BR");
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? builder.HostEnvironment.BaseAddress), Timeout = TimeSpan.FromSeconds(15) });
builder.Services.AddScoped<ADSync.Client.Services.ManutencaoClient>();

await builder.Build().RunAsync();
