namespace UrlPreviewApp.Services;

/// <summary>
/// Settings for fetching third-party pages (configuration section <c>Preview</c>).
/// </summary>
public sealed class PreviewOptions
{
    public const string SectionName = "Preview";

    /// <summary>
    /// Allows requests to loopback, private and link-local addresses. Keep <see langword="false" />
    /// in any deployment: enabling it exposes internal services through <c>/api/metadata</c> (SSRF).
    /// </summary>
    public bool AllowPrivateNetworks { get; set; }

    /// <summary>Maximum time for fetching a page.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Maximum number of bytes read from a page.</summary>
    public long MaxContentBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Maximum time for the robots.txt / sitemap.xml / favicon.ico checks.</summary>
    public int SiteChecksTimeoutSeconds { get; set; } = 5;

    /// <summary>Requests per minute allowed to <c>/api/metadata</c> for each client IP.</summary>
    public int RequestsPerMinute { get; set; } = 30;

    public string UserAgent { get; set; } = "Mozilla/5.0 (compatible; UrlPreviewApp/1.0; +https://github.com/kingsor/UrlPreviewParser)";
}
