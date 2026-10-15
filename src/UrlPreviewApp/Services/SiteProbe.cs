using System.Net;
using UrlPreviewApp.Client.Models;

namespace UrlPreviewApp.Services;

/// <summary>
/// Site-level checks that are not part of the page HTML: robots.txt, sitemap.xml and the
/// /favicon.ico fallback. Runs the requests in parallel; any failure simply means "not found".
/// </summary>
public static class SiteProbe
{
    public static async Task ApplyAsync(HttpClient httpClient, PageMetadata metadata, TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(metadata.FinalUrl ?? metadata.Url, UriKind.Absolute, out var page))
            return;

        var origin = page.GetLeftPart(UriPartial.Authority);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var token = timeoutSource.Token;

        var robots = ExistsAsync(httpClient, $"{origin}/robots.txt", token);
        var sitemap = ExistsAsync(httpClient, $"{origin}/sitemap.xml", token);
        var favicon = metadata.Favicon is null
            ? ExistsAsync(httpClient, $"{origin}/favicon.ico", token)
            : Task.FromResult(false);

        await Task.WhenAll(robots, sitemap, favicon).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        if (await robots)
            metadata.RobotsFile = $"{origin}/robots.txt";
        if (await sitemap)
            metadata.Sitemap = $"{origin}/sitemap.xml";
        if (await favicon)
            metadata.Favicon = $"{origin}/favicon.ico";
    }

    private static async Task<bool> ExistsAsync(HttpClient httpClient, string url, CancellationToken cancellationToken)
    {
        try
        {
            using (var head = new HttpRequestMessage(HttpMethod.Head, url))
            using (var response = await httpClient.SendAsync(head, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
            {
                if (response.StatusCode is not (HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented))
                    return response.IsSuccessStatusCode;
            }

            // Some servers reject HEAD: retry with GET, reading only the headers.
            using var fallback = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            return fallback.IsSuccessStatusCode;
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
        {
            return false;
        }
    }
}
