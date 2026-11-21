using System.Net;
using System.Net.Sockets;

namespace Spikes.WebViewSearch;

/// <summary>Decides whether a URL targets loopback, private, link-local or otherwise local addresses.</summary>
internal static class UrlGuard
{
    private static readonly HashSet<string> WebSchemes = ["http", "https", "ws", "wss", "ftp"];

    public static bool IsWebScheme(Uri u) => WebSchemes.Contains(u.Scheme);

    public static bool IsPrivate(IPAddress a)
    {
        if (a.IsIPv4MappedToIPv6) a = a.MapToIPv4();
        if (IPAddress.IsLoopback(a) || a.Equals(IPAddress.Any) || a.Equals(IPAddress.IPv6Any)) return true;
        if (a.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = a.GetAddressBytes();
            return b[0] == 10 || b[0] == 127 || b[0] == 0
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 169 && b[1] == 254)
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127);
        }
        var v6 = a.GetAddressBytes();
        return a.IsIPv6LinkLocal || a.IsIPv6SiteLocal || (v6[0] & 0xFE) == 0xFC;
    }

    /// <summary>True when the host is "localhost" or an IP literal in a private range (no DNS).</summary>
    public static bool IsPrivateLiteral(Uri u)
    {
        if (!IsWebScheme(u)) return false;
        var host = u.IdnHost.Trim('[', ']');
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)) return true;
        return IPAddress.TryParse(host, out var ip) && IsPrivate(ip);
    }

    public static bool IsIpLiteral(Uri u) => IPAddress.TryParse(u.IdnHost.Trim('[', ']'), out _);
}
