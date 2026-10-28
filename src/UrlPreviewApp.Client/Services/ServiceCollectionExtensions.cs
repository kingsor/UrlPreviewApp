using Microsoft.Extensions.DependencyInjection;
using MudBlazor.Services;

namespace UrlPreviewApp.Client.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the browser-side services (MudBlazor included). Needed on the server too, so components can be prerendered.
    /// </summary>
    public static IServiceCollection AddUrlPreviewBrowserServices(this IServiceCollection services)
    {
        services.AddMudServices();
        services.AddScoped<BrowserInterop>();
        services.AddScoped<RecentUrlsStore>();
        services.AddScoped<ThemePreferenceStore>();
        return services;
    }
}
