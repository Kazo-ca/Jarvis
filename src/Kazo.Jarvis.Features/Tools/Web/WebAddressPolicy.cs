using System.Net;
using System.Net.Sockets;
using Kazo.Jarvis.Kernel.Settings;
using Kazo.Jarvis.Kernel.Web;

namespace Kazo.Jarvis.Features.Tools.Web;

/// <summary>
/// Which addresses the web tools may read (ADR 0017, section 3): <c>http</c> and <c>https</c>, and never this
/// machine nor the local network, unless the user listed the host. The tools use it to class a call; the browser of
/// the host asks it again at every navigation, redirection and resource, once the name is resolved (ADR 0022,
/// section 3).
/// </summary>
public sealed class WebAddressPolicy : IWebAddressPolicy
{
    /// <summary>Endings of names that only exist on a local network or on this machine.</summary>
    private static readonly string[] LocalSuffixes = [".localhost", ".local", ".lan", ".home", ".internal", ".intranet", ".corp", ".home.arpa"];

    private readonly ISettingsStore settings;

    /// <summary>Creates the policy.</summary>
    /// <param name="settings">The settings, for <see cref="WebToolsOptions.Section"/>.</param>
    public WebAddressPolicy(ISettingsStore settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        this.settings = settings;
    }

    /// <inheritdoc />
    public bool Allows(Uri address)
    {
        ArgumentNullException.ThrowIfNull(address);

        // A name and a password in the address are never chosen by a model (ADR 0017: no header nor body of its own).
        if (!address.IsAbsoluteUri || (address.Scheme != Uri.UriSchemeHttp && address.Scheme != Uri.UriSchemeHttps) || address.UserInfo.Length > 0)
        {
            return false;
        }

        var host = address.IdnHost.TrimEnd('.');
        if (host.Length == 0)
        {
            return false;
        }

        if (IsException(host))
        {
            return true;
        }

        if (IPAddress.TryParse(host.Trim('[', ']'), out var literal))
        {
            return !IsLocal(literal);
        }

        // A name without a dot (« router », « homeassistant ») is resolved by the local network only.
        return host.Contains('.', StringComparison.Ordinal)
            && !host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            && !LocalSuffixes.Any(suffix => host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public bool AllowsResolved(string host, IPAddress resolved)
    {
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(resolved);
        return IsException(host.TrimEnd('.')) || !IsLocal(resolved);
    }

    /// <summary>
    /// Whether <paramref name="address"/> is this machine or a network that is not the public Internet: loopback,
    /// private ranges, link-local, carrier-grade NAT (where a VPN such as Tailscale lives), multicast, unspecified.
    /// </summary>
    internal static bool IsLocal(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] switch
            {
                0 or 10 or 127 => true,
                100 => bytes[1] is >= 64 and <= 127,
                169 => bytes[1] == 254,
                172 => bytes[1] is >= 16 and <= 31,
                192 => bytes[1] == 168,
                >= 224 => true,
                _ => false,
            };
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var first = address.GetAddressBytes()[0];
            return address.Equals(IPAddress.IPv6None) || address.Equals(IPAddress.IPv6Any)
                || address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
                || (first & 0xFE) == 0xFC;
        }

        // Another family: nothing the web tools should reach.
        return true;
    }

    private bool IsException(string host) =>
        settings.Read(WebToolsOptions.Section).LocalExceptions
            .Any(exception => string.Equals(exception?.Trim().Trim('[', ']').TrimEnd('.'), host.Trim('[', ']'), StringComparison.OrdinalIgnoreCase));
}
