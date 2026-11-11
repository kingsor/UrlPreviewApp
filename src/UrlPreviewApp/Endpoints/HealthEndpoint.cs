using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using UrlPreviewApp.Client;

namespace UrlPreviewApp.Endpoints;

/// <summary>
/// <c>GET /healthz</c>: liveness probe (DigitalOcean App Platform checks only the status code)
/// that also reports the deployed version, e.g. <c>{"status":"Healthy","version":"1.0.1"}</c>.
/// </summary>
public static class HealthEndpoint
{
    public const string Path = "/healthz";

    public static IEndpointConventionBuilder MapHealthEndpoint(this IEndpointRouteBuilder app) =>
        app.MapHealthChecks(Path, new HealthCheckOptions { ResponseWriter = WriteResponseAsync });

    private static async Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";

        await using var json = new Utf8JsonWriter(context.Response.Body);
        json.WriteStartObject();
        json.WriteString("status", report.Status.ToString());
        json.WriteString("version", AppVersion.Current);
        json.WriteEndObject();
        await json.FlushAsync(context.RequestAborted);
    }
}
