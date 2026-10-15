using Microsoft.Extensions.Options;
using UrlPreviewApp.Client.Logic;
using UrlPreviewApp.Client.Models;
using UrlPreviewApp.Client.Services;
using UrlPreviewParser;
using UrlPreviewParser.Models;

namespace UrlPreviewApp.Services;

/// <summary>
/// Server implementation of <see cref="IPageMetadataProvider"/>, built on UrlPreviewParser.
/// </summary>
public sealed class MetadataService(
    IHttpClientFactory httpClientFactory,
    IOptions<PreviewOptions> options,
    ILogger<MetadataService> logger) : IPageMetadataProvider
{
    public const string HttpClientName = "preview";

    public async Task<MetadataLookupResult> GetMetadataAsync(string url, CancellationToken cancellationToken = default)
    {
        if (!UrlNormalizer.TryNormalize(url, out var normalized))
            return MetadataLookupResult.Failure("Please enter a valid http(s) URL.", StatusCodes.Status400BadRequest);

        var settings = options.Value;
        var httpClient = httpClientFactory.CreateClient(HttpClientName);
        var parserOptions = new UrlPreviewOptions
        {
            Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds),
            MaxContentLength = settings.MaxContentBytes,
            FetchOembed = false,
            ResolveRelativeUrls = true,
        };

        try
        {
            using var client = new UrlPreviewClient(httpClient, parserOptions);
            var preview = await client.GetPreviewAsync(normalized, cancellationToken).ConfigureAwait(false);

            var metadata = MetadataMapper.Map(preview);
            await SiteProbe
                .ApplyAsync(httpClient, metadata, TimeSpan.FromSeconds(settings.SiteChecksTimeoutSeconds), cancellationToken)
                .ConfigureAwait(false);

            return MetadataLookupResult.Success(metadata);
        }
        catch (TimeoutException)
        {
            return MetadataLookupResult.Failure("The site took too long to respond.", StatusCodes.Status504GatewayTimeout);
        }
        catch (HttpRequestException ex) when (NetworkGuard.IsBlocked(ex))
        {
            return MetadataLookupResult.Failure("This address is not allowed: only public websites can be previewed.", StatusCodes.Status400BadRequest);
        }
        catch (HttpRequestException ex) when (ex.StatusCode is { } status)
        {
            return MetadataLookupResult.Failure(
                $"The site responded with {(int)status} {ReasonPhrase(status)}.",
                StatusCodes.Status502BadGateway);
        }
        catch (HttpRequestException ex)
        {
            logger.LogInformation(ex, "Could not reach {Url}", normalized);
            return MetadataLookupResult.Failure("Could not reach the site. Please check the URL and try again.", StatusCodes.Status502BadGateway);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to fetch or parse {Url}", normalized);
            return MetadataLookupResult.Failure("Failed to fetch or parse the website.", StatusCodes.Status500InternalServerError);
        }
    }

    private static string ReasonPhrase(System.Net.HttpStatusCode status) =>
        Microsoft.AspNetCore.WebUtilities.ReasonPhrases.GetReasonPhrase((int)status) is { Length: > 0 } phrase
            ? phrase
            : status.ToString();
}
