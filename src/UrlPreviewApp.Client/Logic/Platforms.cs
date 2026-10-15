using UrlPreviewApp.Client.Models;

namespace UrlPreviewApp.Client.Logic;

public enum Platform
{
    Telegram,
    Discord,
    Slack,
    X,
    Facebook,
    LinkedIn,
    WhatsApp,
}

public enum PreviewLayout
{
    /// <summary>Large 1.91:1 image above the text.</summary>
    LargeImage,

    /// <summary>Small thumbnail next to the site name.</summary>
    CompactIcon,
}

public enum SiteLabelSource
{
    Hostname,
    SiteName,
}

/// <summary>
/// How a platform renders a link card. Colors live in the CSS class <see cref="CssClass"/>.
/// </summary>
public sealed record PlatformTheme(
    Platform Platform,
    string Label,
    string CssClass,
    PreviewLayout Layout,
    SiteLabelSource SiteLabel,
    bool UppercaseSiteLabel,
    int TitleLines,
    int DescriptionLines,
    bool PreferTwitterTags);

public sealed record PreviewContent(string SiteLabel, string Title, string? Description, string? Image);

public static class PlatformThemes
{
    public static IReadOnlyList<PlatformTheme> All { get; } =
    [
        new(Platform.Telegram, "Telegram", "preview--telegram", PreviewLayout.LargeImage, SiteLabelSource.Hostname, false, 1, 2, false),
        new(Platform.Discord, "Discord", "preview--discord", PreviewLayout.CompactIcon, SiteLabelSource.SiteName, false, 1, 2, false),
        new(Platform.Slack, "Slack", "preview--slack", PreviewLayout.CompactIcon, SiteLabelSource.SiteName, false, 1, 2, false),
        new(Platform.X, "X", "preview--x", PreviewLayout.LargeImage, SiteLabelSource.Hostname, false, 2, 2, true),
        new(Platform.Facebook, "Facebook", "preview--facebook", PreviewLayout.LargeImage, SiteLabelSource.SiteName, true, 1, 1, false),
        new(Platform.LinkedIn, "LinkedIn", "preview--linkedin", PreviewLayout.LargeImage, SiteLabelSource.SiteName, false, 2, 2, false),
        new(Platform.WhatsApp, "WhatsApp", "preview--whatsapp", PreviewLayout.LargeImage, SiteLabelSource.SiteName, true, 2, 2, false),
    ];

    public static PlatformTheme Get(Platform platform) => All.First(t => t.Platform == platform);
}

/// <summary>
/// Chooses the title, description and image a platform shows for a link.
/// </summary>
public static class PreviewResolver
{
    public static PreviewContent Resolve(PageMetadata metadata, string url, PlatformTheme theme)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentNullException.ThrowIfNull(theme);

        string? title;
        string? description;
        string? image;

        if (theme.PreferTwitterTags)
        {
            title = FirstNonEmpty(metadata.TwitterTitle, metadata.OgTitle, metadata.Title);
            description = FirstNonEmpty(metadata.TwitterDescription, metadata.OgDescription, metadata.Description);
            image = FirstNonEmpty(metadata.TwitterImage, metadata.OgImage);
        }
        else
        {
            title = FirstNonEmpty(metadata.OgTitle, metadata.Title, metadata.TwitterTitle);
            description = FirstNonEmpty(metadata.OgDescription, metadata.Description, metadata.TwitterDescription);
            image = FirstNonEmpty(metadata.OgImage, metadata.TwitterImage);
        }

        var hostname = UrlNormalizer.GetHostname(url);
        var siteLabel = theme.SiteLabel == SiteLabelSource.SiteName
            ? FirstNonEmpty(metadata.OgSiteName) ?? hostname
            : hostname;

        return new PreviewContent(siteLabel, title ?? url, description, image);
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
}
