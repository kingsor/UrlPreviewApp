using System.Net;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;
using UrlPreviewApp.Client.Models;
using UrlPreviewApp.Client.Services;
using UrlPreviewApp.Components;
using UrlPreviewApp.Endpoints;
using UrlPreviewApp.Services;

// Legacy code pages (windows-1252, …) for pages that are not UTF-8.
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.Configure<PreviewOptions>(builder.Configuration.GetSection(PreviewOptions.SectionName));

builder.Services
    .AddHttpClient(MetadataService.HttpClientName, (services, client) =>
    {
        var options = services.GetRequiredService<IOptions<PreviewOptions>>().Value;
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds + 5);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
        client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml;q=0.9,*/*;q=0.8");
    })
    .ConfigurePrimaryHttpMessageHandler(services =>
    {
        var options = services.GetRequiredService<IOptions<PreviewOptions>>().Value;
        return new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(10),
            ConnectCallback = options.AllowPrivateNetworks ? null : NetworkGuard.ConnectAsync,
        };
    });

builder.Services.AddScoped<MetadataService>();
builder.Services.AddScoped<IPageMetadataProvider>(services => services.GetRequiredService<MetadataService>());
builder.Services.AddUrlPreviewBrowserServices();

builder.Services.ConfigureHttpJsonOptions(json => json.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonContext.Default));

builder.Services.AddRateLimiter(limiter =>
{
    limiter.AddPolicy(MetadataEndpoint.RateLimitPolicy, context =>
    {
        var permits = context.RequestServices.GetRequiredService<IOptions<PreviewOptions>>().Value.RequestsPerMinute;
        return RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permits, Window = TimeSpan.FromMinutes(1) });
    });
    limiter.OnRejected = async (rejected, cancellationToken) =>
    {
        rejected.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await rejected.HttpContext.Response.WriteAsJsonAsync(
            new ApiError("Too many requests. Please wait a minute and try again."),
            AppJsonContext.Default.ApiError,
            cancellationToken: cancellationToken);
    };
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseRateLimiter();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapMetadataEndpoint();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(UrlPreviewApp.Client._Imports).Assembly);

app.Run();

/// <summary>Entry point, exposed for integration tests.</summary>
public partial class Program;
