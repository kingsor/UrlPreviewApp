using UrlPreviewApp.Client.Models;

namespace UrlPreviewApp.Client.Logic;

public sealed record ScoreCheck(string Name, bool Passed, int Weight);

public sealed record ScoreResult(int Score, IReadOnlyList<ScoreCheck> Checks);

/// <summary>
/// Weighted metadata quality score (0–100).
/// </summary>
public static class ScoreCalculator
{
    public static ScoreResult Calculate(PageMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var titleLength = metadata.Title?.Length ?? 0;
        var descriptionLength = metadata.Description?.Length ?? 0;

        ScoreCheck[] checks =
        [
            new("Title", HasValue(metadata.Title), 10),
            new("Description", HasValue(metadata.Description), 10),
            new("OG Title", HasValue(metadata.OgTitle), 8),
            new("OG Description", HasValue(metadata.OgDescription), 8),
            new("OG Image", HasValue(metadata.OgImage), 12),
            new("Favicon", HasValue(metadata.Favicon), 6),
            new("Canonical", HasValue(metadata.Canonical), 6),
            new("Viewport", HasValue(metadata.Viewport), 6),
            new("Twitter Card", HasValue(metadata.TwitterCard), 8),
            new("OG Type", HasValue(metadata.OgType), 6),
            new("OG Locale", HasValue(metadata.OgLocale), 4),
            new("robots.txt", HasValue(metadata.RobotsFile), 2),
            new("sitemap.xml", HasValue(metadata.Sitemap), 2),
            new("Title Length", titleLength is >= MetadataLimits.TitleMinChars and <= MetadataLimits.TitleMaxChars, 4),
            new("Desc Length", descriptionLength is >= MetadataLimits.DescriptionMinChars and <= MetadataLimits.DescriptionMaxChars, 4),
        ];

        var total = checks.Sum(c => c.Weight);
        var earned = checks.Where(c => c.Passed).Sum(c => c.Weight);
        var score = (int)Math.Round(earned * 100d / total, MidpointRounding.AwayFromZero);

        return new ScoreResult(score, checks);
    }

    private static bool HasValue(string? value) => !string.IsNullOrWhiteSpace(value);
}
