using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace UrlPreviewApp.Client.Logic;

/// <summary>
/// Normalization and display helpers for user-entered URLs.
/// </summary>
public static class UrlNormalizer
{
    /// <summary>
    /// Turns user input into an absolute http(s) URL: trims it, adds <c>https://</c> when no scheme is given
    /// and lower-cases scheme and host. Returns <see langword="false" /> for anything that is not a plausible web URL.
    /// </summary>
    public static bool TryNormalize(string? input, [NotNullWhen(true)] out string? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var candidate = input.Trim();
        var hasHttpScheme = candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        if (!hasHttpScheme)
        {
            if (candidate.Contains("://", StringComparison.Ordinal))
                return false;

            candidate = "https://" + candidate.TrimStart('/');
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !IsPlausibleHost(uri))
        {
            return false;
        }

        normalized = uri.AbsoluteUri;
        return true;
    }

    /// <summary>Host name without a leading <c>www.</c>, or the input when it is not a URL.</summary>
    public static string GetHostname(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
    }

    /// <summary>Host and path, without scheme, query or fragment.</summary>
    public static string GetDisplayUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return url;

        return uri.AbsolutePath == "/" ? uri.Host : uri.Host + uri.AbsolutePath;
    }

    /// <summary>Whether <paramref name="url"/> can safely be used in an <c>href</c> (absolute http/https).</summary>
    public static bool IsHttpUrl([NotNullWhen(true)] string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool IsPlausibleHost(Uri uri) =>
        uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6
        || (uri.HostNameType == UriHostNameType.Dns
            && (uri.Host.Contains('.') || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)))
        || IPAddress.TryParse(uri.Host, out _);
}
