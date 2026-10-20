using System.Globalization;
using Chagi.Core.Data;
using Chagi.Web;
using Chagi.Web.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var culture = CultureInfo.GetCultureInfo("sv-SE");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(_ => new HttpClient(new BrowserRevalidationHandler()) { Timeout = TimeSpan.FromSeconds(30) });
builder.Services.AddScoped<IDataSnapshotStore, BrowserSnapshotStore>();
builder.Services.AddScoped(sp => new PublishedResultsClient(sp.GetRequiredService<HttpClient>(),
    new Uri(builder.Configuration["Data:BaseUrl"] ?? throw new InvalidOperationException("Data:BaseUrl saknas.")),
    sp.GetRequiredService<IDataSnapshotStore>()));
builder.Services.AddScoped<PublicImages>();
await builder.Build().RunAsync();
