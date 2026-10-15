using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using UrlPreviewApp.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<IPageMetadataProvider, MetadataApiClient>();
builder.Services.AddUrlPreviewBrowserServices();

await builder.Build().RunAsync();
