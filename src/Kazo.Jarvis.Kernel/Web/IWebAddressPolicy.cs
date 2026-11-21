using System.Net;

namespace Kazo.Jarvis.Kernel.Web;

/// <summary>
/// Which addresses Jarvis may read on the web (ADR 0017, section 3; ADR 0022, section 3): <c>http</c> and
/// <c>https</c> only, and never this machine or the local network, so that a trapped page cannot make a model read
/// Home Assistant, the local server of Jarvis or a router. The slice of the web tools implements it with the
/// exceptions the user set; the browser of the host asks it at every navigation, redirection and resource.
/// </summary>
public interface IWebAddressPolicy
{
    /// <summary>Whether <paramref name="address"/> may be read, from what is written in it: its scheme and its host.</summary>
    /// <param name="address">An absolute address.</param>
    /// <returns><see langword="false"/> for another scheme, for a local name or for a local address written as numbers.</returns>
    bool Allows(Uri address);

    /// <summary>Whether a host may be read once its name resolved to <paramref name="resolved"/>.</summary>
    /// <param name="host">The host name of the address.</param>
    /// <param name="resolved">One address the name resolves to.</param>
    /// <returns><see langword="false"/> for the loopback, the private ranges and the link-local ranges, unless the user listed the host.</returns>
    bool AllowsResolved(string host, IPAddress resolved);
}
