namespace UrlPreviewApp.Client.Models;

/// <summary>
/// Link preview metadata of a page, as returned by <c>GET /api/metadata</c>.
/// All URL-valued properties are absolute.
/// </summary>
public sealed class PageMetadata
{
    public string Url { get; set; } = string.Empty;
    public string? FinalUrl { get; set; }

    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Canonical { get; set; }
    public string? Favicon { get; set; }
    public string? Language { get; set; }
    public string? Charset { get; set; }
    public string? Viewport { get; set; }
    public string? Robots { get; set; }
    public string? ThemeColor { get; set; }
    public string? Generator { get; set; }

    public string? OgTitle { get; set; }
    public string? OgDescription { get; set; }
    public string? OgImage { get; set; }
    public string? OgType { get; set; }
    public string? OgUrl { get; set; }
    public string? OgSiteName { get; set; }
    public string? OgLocale { get; set; }

    public string? TwitterCard { get; set; }
    public string? TwitterTitle { get; set; }
    public string? TwitterDescription { get; set; }
    public string? TwitterImage { get; set; }
    public string? TwitterSite { get; set; }
    public string? TwitterCreator { get; set; }

    /// <summary>URL of <c>/robots.txt</c> when it exists.</summary>
    public string? RobotsFile { get; set; }

    /// <summary>URL of <c>/sitemap.xml</c> when it exists.</summary>
    public string? Sitemap { get; set; }

    /// <summary>Every element of <c>&lt;head&gt;</c> that has attributes, with raw attribute values.</summary>
    public List<HeadTag> HeadTags { get; set; } = [];
}

/// <summary>
/// A raw <c>&lt;head&gt;</c> element.
/// </summary>
public sealed class HeadTag
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string?> Attributes { get; set; } = new();
}

/// <summary>
/// Error payload returned by the API.
/// </summary>
public sealed record ApiError(string Error);

/// <summary>
/// Outcome of a metadata lookup.
/// </summary>
public sealed record MetadataLookupResult(PageMetadata? Metadata, string? Error, int StatusCode)
{
    public static MetadataLookupResult Success(PageMetadata metadata) => new(metadata, null, 200);

    public static MetadataLookupResult Failure(string error, int statusCode) => new(null, error, statusCode);
}
