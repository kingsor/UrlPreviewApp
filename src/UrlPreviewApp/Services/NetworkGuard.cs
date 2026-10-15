using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace UrlPreviewApp.Services;

/// <summary>
/// Thrown when a request would connect to a non-public address.
/// </summary>
public sealed class BlockedAddressException(string host)
    : Exception($"Connections to '{host}' are not allowed.");

/// <summary>
/// SSRF protection: a <see cref="SocketsHttpHandler.ConnectCallback"/> that resolves the host and connects only
/// to public addresses. Because the check runs on the resolved IP for every connection (redirects included),
/// it is not fooled by DNS names pointing to internal addresses.
/// </summary>
public static class NetworkGuard
{
    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken).ConfigureAwait(false);

        var allowed = addresses.Where(IsPublic).ToArray();
        if (allowed.Length == 0)
            throw new BlockedAddressException(host);

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>Whether <paramref name="exception"/> (or one of its inner exceptions) is a blocked connection.</summary>
    public static bool IsBlocked(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is BlockedAddressException)
                return true;
        }

        return false;
    }

    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address))
            return false;

        var b = address.GetAddressBytes();

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return !(b[0] == 0                                    // 0.0.0.0/8 "this network"
                || b[0] == 10                                     // 10.0.0.0/8 private
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)     // 100.64.0.0/10 carrier-grade NAT
                || (b[0] == 169 && b[1] == 254)                   // 169.254.0.0/16 link-local (cloud metadata)
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)      // 172.16.0.0/12 private
                || (b[0] == 192 && b[1] == 0 && b[2] == 0)        // 192.0.0.0/24 IETF protocol assignments
                || (b[0] == 192 && b[1] == 168)                   // 192.168.0.0/16 private
                || (b[0] == 198 && (b[1] == 18 || b[1] == 19))    // 198.18.0.0/15 benchmarking
                || b[0] >= 224);                                  // multicast, reserved, broadcast
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return !(address.Equals(IPAddress.IPv6Any)
                || address.IsIPv6LinkLocal
                || address.IsIPv6SiteLocal
                || address.IsIPv6Multicast
                || (b[0] & 0xFE) == 0xFC                          // fc00::/7 unique local
                || (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xFF && b[3] == 0x9B)); // 64:ff9b::/96 NAT64
        }

        return false;
    }
}
