using UrlPreviewApp.Client.Models;
using UrlPreviewParser.Models;

namespace UrlPreviewApp.Services;

/// <summary>
/// Converts a UrlPreviewParser result into the flat contract sent to the browser
/// (without the raw HTML body).
/// </summary>
public static class MetadataMapper
{
    private const string TagNameKey = "tag-name";

    public static PageMetadata Map(UrlPreviewResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var og = result.OpenGraph;
        var twitter = result.TwitterCard;
        var tags = result.Tags;

        return new PageMetadata
        {
            Url = result.Url,
            FinalUrl = Clean(result.FinalUrl),

            Title = Clean(tags.Title),
            Description = Clean(tags.Description),
            Canonical = Clean(tags.Canonical),
            Favicon = Clean(tags.Favicon),
            Language = Clean(tags.Language),
            Charset = Clean(tags.Charset),
            Viewport = Clean(tags.Viewport),
            Robots = Clean(tags.Robots),
            ThemeColor = Clean(tags.ThemeColor),
            Generator = Clean(tags.Generator),

            OgTitle = Clean(og.Title),
            OgDescription = Clean(og.Description),
            OgImage = Clean(og.Image) ?? Get(og, "og:image:url") ?? Get(og, "og:image:secure_url"),
            OgType = Clean(og.Type),
            OgUrl = Clean(og.Url),
            OgSiteName = Clean(og.SiteName),
            OgLocale = Get(og, "og:locale"),

            TwitterCard = Clean(twitter.Card),
            TwitterTitle = Clean(twitter.Title),
            TwitterDescription = Clean(twitter.Description),
            TwitterImage = Clean(twitter.Image),
            TwitterSite = Clean(twitter.Site),
            TwitterCreator = Clean(twitter.Creator),

            HeadTags = result.Head.Select(ToHeadTag).ToList(),
        };
    }

    private static HeadTag ToHeadTag(Dictionary<string, string?> element) => new()
    {
        Name = element.TryGetValue(TagNameKey, out var name) ? name ?? string.Empty : string.Empty,
        Attributes = element
            .Where(attribute => attribute.Key != TagNameKey)
            .ToDictionary(attribute => attribute.Key, attribute => attribute.Value),
    };

    private static string? Get(Dictionary<string, string> values, string key) =>
        values.TryGetValue(key, out var value) ? Clean(value) : null;

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
