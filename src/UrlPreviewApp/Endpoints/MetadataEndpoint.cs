using UrlPreviewApp.Client.Models;
using UrlPreviewApp.Services;

namespace UrlPreviewApp.Endpoints;

public static class MetadataEndpoint
{
    public const string RateLimitPolicy = "metadata";

    /// <summary>
    /// <c>GET /api/metadata?url=…</c> → <see cref="PageMetadata"/>, or <see cref="ApiError"/> with a 4xx/5xx status.
    /// </summary>
    public static IEndpointRouteBuilder MapMetadataEndpoint(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/metadata", async (string? url, MetadataService service, HttpContext context, CancellationToken cancellationToken) =>
            {
                context.Response.Headers.CacheControl = "no-store";

                var result = await service.GetMetadataAsync(url ?? string.Empty, cancellationToken);
                return result.Metadata is { } metadata
                    ? Results.Json(metadata, AppJsonContext.Default.PageMetadata)
                    : Results.Json(new ApiError(result.Error ?? "Unexpected error."), AppJsonContext.Default.ApiError, statusCode: result.StatusCode);
            })
            .RequireRateLimiting(RateLimitPolicy)
            .WithName("GetMetadata");

        return endpoints;
    }
}
