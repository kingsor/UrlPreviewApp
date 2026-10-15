using System.Text.Json;
using UrlPreviewApp.Client.Logic;
using UrlPreviewApp.Client.Models;

namespace UrlPreviewApp.Tests;

public class UrlNormalizerTests
{
    [Theory]
    [InlineData("example.com", "https://example.com/")]
    [InlineData("  Example.COM/Path?q=1#top ", "https://example.com/Path?q=1#top")]
    [InlineData("http://example.com", "http://example.com/")]
    [InlineData("https://sub.example.co.uk/a/b", "https://sub.example.co.uk/a/b")]
    [InlineData("localhost:5000", "https://localhost:5000/")]
    [InlineData("192.168.1.10", "https://192.168.1.10/")]
    public void TryNormalize_AcceptsWebUrls(string input, string expected)
    {
        Assert.True(UrlNormalizer.TryNormalize(input, out var normalized));
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("intranet")]
    [InlineData("ftp://example.com")]
    [InlineData("javascript:alert(1)")]
    public void TryNormalize_RejectsInvalidInput(string input)
    {
        Assert.False(UrlNormalizer.TryNormalize(input, out _));
    }

    [Fact]
    public void GetHostname_StripsWww()
    {
        Assert.Equal("example.com", UrlNormalizer.GetHostname("https://www.example.com/page"));
    }

    [Fact]
    public void GetDisplayUrl_ReturnsHostAndPath()
    {
        Assert.Equal("example.com/blog/post", UrlNormalizer.GetDisplayUrl("https://example.com/blog/post?x=1"));
        Assert.Equal("example.com", UrlNormalizer.GetDisplayUrl("https://example.com/"));
    }

    [Theory]
    [InlineData("https://example.com", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html,hi", false)]
    [InlineData("/relative", false)]
    [InlineData(null, false)]
    public void IsHttpUrl_OnlyAllowsHttpSchemes(string? url, bool expected)
    {
        Assert.Equal(expected, UrlNormalizer.IsHttpUrl(url));
    }
}

public class ScoreCalculatorTests
{
    [Fact]
    public void Calculate_ReturnsZeroForEmptyMetadata()
    {
        var result = ScoreCalculator.Calculate(new PageMetadata());

        Assert.Equal(0, result.Score);
        Assert.All(result.Checks, check => Assert.False(check.Passed));
    }

    [Fact]
    public void Calculate_ReturnsHundredForCompleteMetadata()
    {
        var result = ScoreCalculator.Calculate(CompleteMetadata());

        Assert.Equal(100, result.Score);
        Assert.Equal(15, result.Checks.Count);
    }

    [Fact]
    public void Calculate_WeighsChecks()
    {
        var metadata = new PageMetadata { OgImage = "https://example.com/og.png", Title = "Short" };

        var result = ScoreCalculator.Calculate(metadata);

        // (OG Image 12 + Title 10) / 96 = 22.9 → 23
        Assert.Equal(23, result.Score);
        Assert.False(result.Checks.Single(c => c.Name == "Title Length").Passed);
    }

    internal static PageMetadata CompleteMetadata() => new()
    {
        Title = new string('t', 45),
        Description = new string('d', 140),
        OgTitle = "OG title",
        OgDescription = "OG description",
        OgImage = "https://example.com/og.png",
        Favicon = "https://example.com/favicon.ico",
        Canonical = "https://example.com/",
        Viewport = "width=device-width",
        TwitterCard = "summary_large_image",
        OgType = "website",
        OgLocale = "en_US",
        RobotsFile = "https://example.com/robots.txt",
        Sitemap = "https://example.com/sitemap.xml",
    };
}

public class HtmlTagGeneratorTests
{
    [Fact]
    public void Generate_PreservesOriginalAttributesAndEscapes()
    {
        var metadata = new PageMetadata
        {
            Title = "Tom & \"Jerry\"",
            HeadTags =
            [
                new HeadTag { Name = "meta", Attributes = new() { ["property"] = "og:site_name", ["content"] = "<Site>" } },
                new HeadTag { Name = "link", Attributes = new() { ["rel"] = "canonical", ["href"] = "https://example.com/?a=1&b=2" } },
                new HeadTag { Name = "script", Attributes = new() { ["src"] = "/app.js" } },
                new HeadTag { Name = "meta", Attributes = new() { ["name"] = "flag", ["async"] = null } },
            ],
        };

        var html = HtmlTagGenerator.Generate(metadata);

        Assert.Equal(
            """
            <title>Tom &amp; &quot;Jerry&quot;</title>
            <meta property="og:site_name" content="&lt;Site&gt;">
            <link rel="canonical" href="https://example.com/?a=1&amp;b=2">
            <meta name="flag" async>
            """.ReplaceLineEndings("\n"),
            html);
    }
}

public class PreviewResolverTests
{
    private static readonly PageMetadata Metadata = new()
    {
        Title = "Page title",
        OgTitle = "OG title",
        OgDescription = "OG description",
        OgImage = "https://example.com/og.png",
        OgSiteName = "Example Site",
        TwitterTitle = "Twitter title",
        TwitterImage = "https://example.com/tw.png",
    };

    [Fact]
    public void Resolve_PrefersOpenGraphOnMostPlatforms()
    {
        var content = PreviewResolver.Resolve(Metadata, "https://www.example.com/", PlatformThemes.Get(Platform.Facebook));

        Assert.Equal("OG title", content.Title);
        Assert.Equal("OG description", content.Description);
        Assert.Equal("https://example.com/og.png", content.Image);
        Assert.Equal("Example Site", content.SiteLabel);
    }

    [Fact]
    public void Resolve_PrefersTwitterTagsOnX()
    {
        var content = PreviewResolver.Resolve(Metadata, "https://www.example.com/", PlatformThemes.Get(Platform.X));

        Assert.Equal("Twitter title", content.Title);
        Assert.Equal("OG description", content.Description);
        Assert.Equal("https://example.com/tw.png", content.Image);
        Assert.Equal("example.com", content.SiteLabel);
    }

    [Fact]
    public void Resolve_FallsBackToUrlAndHostname()
    {
        var content = PreviewResolver.Resolve(new PageMetadata(), "https://www.example.com/a", PlatformThemes.Get(Platform.Discord));

        Assert.Equal("https://www.example.com/a", content.Title);
        Assert.Null(content.Description);
        Assert.Null(content.Image);
        Assert.Equal("example.com", content.SiteLabel);
    }

    [Fact]
    public void Themes_CoverEveryPlatform()
    {
        Assert.Equal(Enum.GetValues<Platform>().Length, PlatformThemes.All.Count);
    }
}

public class ContractTests
{
    [Fact]
    public void PageMetadata_RoundTripsThroughSourceGeneratedJson()
    {
        var metadata = ScoreCalculatorTests.CompleteMetadata();
        metadata.HeadTags = [new HeadTag { Name = "meta", Attributes = new() { ["charset"] = "utf-8" } }];

        var json = JsonSerializer.Serialize(metadata, AppJsonContext.Default.PageMetadata);
        var copy = JsonSerializer.Deserialize(json, AppJsonContext.Default.PageMetadata)!;

        Assert.Contains("\"ogImage\":", json);
        Assert.DoesNotContain("\"twitterSite\"", json);
        Assert.Equal(metadata.OgImage, copy.OgImage);
        Assert.Equal("utf-8", copy.HeadTags.Single().Attributes["charset"]);
    }
}
