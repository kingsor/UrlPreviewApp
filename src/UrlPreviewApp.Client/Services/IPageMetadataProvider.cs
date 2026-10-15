using UrlPreviewApp.Client.Models;

namespace UrlPreviewApp.Client.Services;

/// <summary>
/// Retrieves the link preview metadata of a URL. Implemented over HTTP in the browser
/// and directly with UrlPreviewParser on the server (used while prerendering).
/// </summary>
public interface IPageMetadataProvider
{
    Task<MetadataLookupResult> GetMetadataAsync(string url, CancellationToken cancellationToken = default);
}
