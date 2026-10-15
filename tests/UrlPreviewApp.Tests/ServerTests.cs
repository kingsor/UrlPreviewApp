using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UrlPreviewApp.Client.Models;
using UrlPreviewApp.Services;
using UrlPreviewParser;

namespace UrlPreviewApp.Tests;

public class MetadataMapperTests
{
    [Fact]
    public async Task Map_FlattensParserResultWithAbsoluteUrls()
    {
        const string html = """
            <html lang="en">
            <head>
                <meta charset="utf-8">
                <title>Example page</title>
                <meta name="description" content="A description">
                <meta property="og:title" content="OG title">
                <meta property="og:image" content="/images/og.png">
                <meta property="og:locale" content="en_US">
                <meta property="og:site_name" content="   ">
                <meta property="twitter:card" content="summary_large_image">
                <link rel="icon" href="favicon.svg">
                <link rel="canonical" href="/post">
            </head>
            <body></body>
            </html>
            """;

        using var client = new UrlPreviewClient();
        var result = await client.ParseAsync("https://example.com/blog/post", html);

        var metadata = MetadataMapper.Map(result);

        Assert.Equal("Example page", metadata.Title);
        Assert.Equal("A description", metadata.Description);
        Assert.Equal("OG title", metadata.OgTitle);
        Assert.Equal("https://example.com/images/og.png", metadata.OgImage);
        Assert.Equal("en_US", metadata.OgLocale);
        Assert.Null(metadata.OgSiteName);
        Assert.Equal("summary_large_image", metadata.TwitterCard);
        Assert.Equal("https://example.com/blog/favicon.svg", metadata.Favicon);
        Assert.Equal("https://example.com/post", metadata.Canonical);
        Assert.Equal("en", metadata.Language);
        Assert.Equal("utf-8", metadata.Charset);
        Assert.Contains(metadata.HeadTags, t => t.Name == "link" && t.Attributes["href"] == "favicon.svg");
        Assert.DoesNotContain(metadata.HeadTags, t => t.Attributes.ContainsKey("tag-name"));
    }
}

public class NetworkGuardTests
{
    [Theory]
    [InlineData("8.8.8.8", true)]
    [InlineData("2606:4700:4700::1111", true)]
    [InlineData("127.0.0.1", false)]
    [InlineData("10.1.2.3", false)]
    [InlineData("172.16.0.1", false)]
    [InlineData("172.31.255.255", false)]
    [InlineData("172.32.0.1", true)]
    [InlineData("192.168.0.10", false)]
    [InlineData("169.254.169.254", false)]
    [InlineData("100.64.0.1", false)]
    [InlineData("0.0.0.0", false)]
    [InlineData("224.0.0.1", false)]
    [InlineData("::1", false)]
    [InlineData("fe80::1", false)]
    [InlineData("fd00::1", false)]
    [InlineData("::ffff:127.0.0.1", false)]
    [InlineData("::ffff:8.8.8.8", true)]
    public void IsPublic_ClassifiesAddresses(string address, bool expected)
    {
        Assert.Equal(expected, NetworkGuard.IsPublic(IPAddress.Parse(address)));
    }
}

public class MetadataServiceTests
{
    private const string PageHtml = """
        <html><head><title>Hello</title><meta property="og:image" content="/og.png"></head><body></body></html>
        """;

    [Fact]
    public async Task GetMetadataAsync_ReturnsMetadataAndSiteChecks()
    {
        var requests = new List<string>();
        var service = CreateService(request =>
        {
            requests.Add($"{request.Method} {request.RequestUri!.AbsolutePath}");
            return request.RequestUri.AbsolutePath switch
            {
                "/page" => Html(PageHtml),
                "/robots.txt" => new HttpResponseMessage(HttpStatusCode.OK),
                "/sitemap.xml" => new HttpResponseMessage(HttpStatusCode.NotFound),
                "/favicon.ico" when request.Method == HttpMethod.Head => new HttpResponseMessage(HttpStatusCode.MethodNotAllowed),
                "/favicon.ico" => new HttpResponseMessage(HttpStatusCode.OK),
                _ => new HttpResponseMessage(HttpStatusCode.NotFound),
            };
        });

        var result = await service.GetMetadataAsync("example.com/page");

        Assert.NotNull(result.Metadata);
        Assert.Equal("Hello", result.Metadata.Title);
        Assert.Equal("https://example.com/og.png", result.Metadata.OgImage);
        Assert.Equal("https://example.com/robots.txt", result.Metadata.RobotsFile);
        Assert.Null(result.Metadata.Sitemap);
        Assert.Equal("https://example.com/favicon.ico", result.Metadata.Favicon);
        Assert.Contains("GET /favicon.ico", requests);
    }

    [Fact]
    public async Task GetMetadataAsync_RejectsInvalidUrl()
    {
        var service = CreateService(_ => throw new InvalidOperationException("No request expected"));

        var result = await service.GetMetadataAsync("not a url");

        Assert.Null(result.Metadata);
        Assert.Equal(400, result.StatusCode);
    }

    [Fact]
    public async Task GetMetadataAsync_ReportsUpstreamStatus()
    {
        var service = CreateService(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));

        var result = await service.GetMetadataAsync("https://example.com");

        Assert.Equal(502, result.StatusCode);
        Assert.Equal("The site responded with 403 Forbidden.", result.Error);
    }

    [Fact]
    public async Task GetMetadataAsync_ReportsBlockedAddress()
    {
        var service = CreateService(_ => throw new HttpRequestException("blocked", new BlockedAddressException("10.0.0.1")));

        var result = await service.GetMetadataAsync("https://internal.example.com");

        Assert.Equal(400, result.StatusCode);
        Assert.Contains("not allowed", result.Error);
    }

    private static MetadataService CreateService(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var factory = new StubHttpClientFactory(new StubHandler(respond));
        return new MetadataService(factory, Options.Create(new PreviewOptions()), NullLogger<MetadataService>.Instance);
    }

    private static HttpResponseMessage Html(string html) =>
        new(HttpStatusCode.OK) { Content = new StringContent(html, Encoding.UTF8, "text/html") };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = respond(request);
            response.RequestMessage ??= request;
            return Task.FromResult(response);
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}

public class MetadataEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Get_ReturnsBadRequestForInvalidUrl()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/metadata?url=not%20a%20url");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.ApiError);
        Assert.Equal("Please enter a valid http(s) URL.", error!.Error);
    }

    [Theory]
    [InlineData("http://127.0.0.1:9/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    [InlineData("http://localhost:9/")]
    public async Task Get_BlocksPrivateAddresses(string url)
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/metadata?url={Uri.EscapeDataString(url)}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync(AppJsonContext.Default.ApiError);
        Assert.Contains("not allowed", error!.Error);
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
    }
}
