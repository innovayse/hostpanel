using System.Net;
using System.Net.Sockets;

namespace Innovayse.Hostpanel.Infrastructure.Plugins.Registry;

/// <summary>
/// Opens the registry client's connections itself so it can refuse a destination the registry address must never reach:
/// loopback, link-local (which holds the cloud metadata address), unspecified, multicast and the other well-known
/// metadata addresses. The name is resolved here and the connection goes to an address that was checked, so a name
/// that resolves to a forbidden address (or to one allowed and one forbidden) cannot slip through, and a redirect or an
/// edited DNS record cannot turn the registry call into a request against the host's own services. Private network
/// ranges stay allowed: a registry on an operator's internal network is a legitimate setup.
/// </summary>
public static class PluginRegistryConnector
{
    /// <summary>Cloud metadata addresses outside the link-local range.</summary>
    private static readonly IPAddress[] MetadataAddresses =
    [
        IPAddress.Parse("100.100.100.200"),
        IPAddress.Parse("168.63.129.16"),
        IPAddress.Parse("fd00:ec2::254"),
    ];

    /// <summary>Tells whether the registry client must never connect to an address.</summary>
    /// <param name="address">The resolved address.</param>
    /// <returns><see langword="true"/> for loopback, link-local, unspecified, broadcast, multicast and metadata addresses.</returns>
    public static bool IsForbidden(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        // An IPv4 address written as IPv6 (::ffff:127.0.0.1) is judged as the IPv4 address it stands for.
        var ip = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

        if (IPAddress.IsLoopback(ip)
            || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any)
            || ip.Equals(IPAddress.Broadcast) || ip.Equals(IPAddress.None)
            || MetadataAddresses.Any(ip.Equals))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast;
        }

        var bytes = ip.GetAddressBytes();
        return (bytes[0] == 169 && bytes[1] == 254) || bytes[0] is >= 224 and <= 239 || bytes[0] == 0;
    }

    /// <summary>The connect callback of the registry client's HTTP handler.</summary>
    /// <param name="context">The destination the handler wants to reach.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A connected stream; the handler adds TLS on top.</returns>
    /// <exception cref="HttpRequestException">The name resolves to nothing, to a forbidden address, or no address accepts the connection.</exception>
    public static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);

        var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
        if (addresses.Length == 0 || addresses.Any(IsForbidden))
        {
            throw new HttpRequestException("The registry address is not allowed.");
        }

        Exception? last = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException ex)
            {
                socket.Dispose();
                last = ex;
            }
        }

        throw new HttpRequestException("The registry could not be reached.", last);
    }
}
