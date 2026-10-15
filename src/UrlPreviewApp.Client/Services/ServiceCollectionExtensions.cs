using Microsoft.Extensions.DependencyInjection;

namespace UrlPreviewApp.Client.Services;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the browser-side services. Needed on the server too, so components can be prerendered.
    /// </summary>
    public static IServiceCollection AddUrlPreviewBrowserServices(this IServiceCollection services)
    {
        services.AddScoped<BrowserInterop>();
        services.AddScoped<RecentUrlsStore>();
        return services;
    }
}
