using System.Net;
using System.Net.Sockets;
using Innovayse.Hostpanel.Shared.Infrastructure.Enums;

namespace Innovayse.Hostpanel.Shared.Infrastructure;

/// <summary>
/// Classifies an address an outbound client is about to dial: public, private network, or never
/// allowed. The one table of ranges every guarded outbound connection is checked against, and the one an address an operator
/// types in is checked against before it is stored.
/// </summary>
public static class OutboundAddressPolicy
{
    /// <summary>
    /// Cloud metadata endpoints that sit inside a range the classification would otherwise call
    /// private (IPv6 unique local, carrier-grade NAT), so they are named one by one.
    /// </summary>
    private static readonly IPAddress[] MetadataEndpoints =
    [
        IPAddress.Parse("fd00:ec2::254"),
        IPAddress.Parse("100.100.100.200"),
    ];

    /// <summary>Classifies one resolved address.</summary>
    /// <param name="address">The address about to be dialled.</param>
    /// <returns>The class the address falls in.</returns>
    public static OutboundAddressClass Classify(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (IPAddress.IsLoopback(address)
            || address.Equals(IPAddress.Any)
            || address.Equals(IPAddress.IPv6Any)
            || address.Equals(IPAddress.Broadcast)
            || MetadataEndpoints.Contains(address))
        {
            return OutboundAddressClass.Refused;
        }

        return address.AddressFamily == AddressFamily.InterNetworkV6 ? ClassifyV6(address) : ClassifyV4(address);
    }

    /// <summary>Classifies an IPv6 address that is not loopback, unspecified or a named metadata endpoint.</summary>
    /// <param name="address">The resolved address to classify.</param>
    /// <returns>The class the address falls in.</returns>
    private static OutboundAddressClass ClassifyV6(IPAddress address)
    {
        if (address.IsIPv6LinkLocal || address.IsIPv6Multicast)
        {
            return OutboundAddressClass.Refused;
        }

        // fc00::/7 is the unique local range, the IPv6 counterpart of the private ranges; fec0::/10
        // is its deprecated site-local predecessor.
        var first = address.GetAddressBytes()[0];
        return address.IsIPv6SiteLocal || (first & 0xFE) == 0xFC
            ? OutboundAddressClass.Private
            : OutboundAddressClass.Public;
    }

    /// <summary>Classifies an IPv4 address that is not loopback, unspecified or broadcast.</summary>
    /// <param name="address">The resolved address to classify.</param>
    /// <returns>The class the address falls in.</returns>
    private static OutboundAddressClass ClassifyV4(IPAddress address)
    {
        var b = address.GetAddressBytes();

        // 0.0.0.0/8 is "this network", 169.254.0.0/16 link-local and the cloud metadata endpoint,
        // 224.0.0.0/3 multicast and reserved.
        if (b[0] == 0 || (b[0] == 169 && b[1] == 254) || b[0] >= 224)
        {
            return OutboundAddressClass.Refused;
        }

        var isPrivate = b[0] == 10
            || (b[0] == 100 && b[1] >= 64 && b[1] <= 127)
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168);
        return isPrivate ? OutboundAddressClass.Private : OutboundAddressClass.Public;
    }
}
