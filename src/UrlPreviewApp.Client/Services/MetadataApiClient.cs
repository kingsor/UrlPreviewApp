using System.Net.Http.Json;
using System.Text.Json;
using UrlPreviewApp.Client.Models;

namespace UrlPreviewApp.Client.Services;

/// <summary>
/// Browser implementation of <see cref="IPageMetadataProvider"/>: calls <c>GET api/metadata</c>.
/// </summary>
public sealed class MetadataApiClient(HttpClient httpClient) : IPageMetadataProvider
{
    private const string GenericError = "Error fetching metadata. Please check the URL and try again.";

    public async Task<MetadataLookupResult> GetMetadataAsync(string url, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient
                .GetAsync($"api/metadata?url={Uri.EscapeDataString(url)}", cancellationToken)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var metadata = await response.Content
                    .ReadFromJsonAsync(AppJsonContext.Default.PageMetadata, cancellationToken)
                    .ConfigureAwait(false);

                return metadata is null
                    ? MetadataLookupResult.Failure(GenericError, 500)
                    : MetadataLookupResult.Success(metadata);
            }

            var error = await TryReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
            return MetadataLookupResult.Failure(error ?? GenericError, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or NotSupportedException)
        {
            return MetadataLookupResult.Failure(GenericError, 0);
        }
    }

    private static async Task<string?> TryReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            return "Too many requests. Please wait a minute and try again.";

        try
        {
            var error = await response.Content
                .ReadFromJsonAsync(AppJsonContext.Default.ApiError, cancellationToken)
                .ConfigureAwait(false);
            return error?.Error;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return null;
        }
    }
}
