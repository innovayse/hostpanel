using System.Globalization;
using System.Net;
using System.Net.Sockets;

namespace Innovayse.Hostpanel.Domain.Fraud;

/// <summary>A range of IP addresses in CIDR notation, e.g. <c>203.0.113.0/24</c>.</summary>
public sealed class IpNetwork
{
    /// <summary>Bits in an IPv4 address.</summary>
    private const int Ipv4Bits = 32;

    /// <summary>Bits in an IPv6 address.</summary>
    private const int Ipv6Bits = 128;

    /// <summary>Bits in one byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The address bytes with the host bits cleared.</summary>
    private readonly byte[] _network;

    /// <summary>Initializes the range.</summary>
    /// <param name="network">The address bytes with the host bits already cleared.</param>
    /// <param name="prefixLength">Number of leading bits that name the network.</param>
    private IpNetwork(byte[] network, int prefixLength)
    {
        _network = network;
        PrefixLength = prefixLength;
    }

    /// <summary>Gets the number of leading bits that name the network.</summary>
    public int PrefixLength { get; }

    /// <summary>Reads a range such as <c>10.0.0.0/8</c> or <c>2001:db8::/32</c>.</summary>
    /// <param name="text">The text to read; host bits set in the address are cleared.</param>
    /// <param name="network">The range when the text is one.</param>
    /// <returns><see langword="true"/> when the text is a valid range.</returns>
    public static bool TryParse(string? text, out IpNetwork network)
    {
        network = null!;
        var parts = (text ?? string.Empty).Trim().Split('/');
        if (parts.Length != 2
            || !IPAddress.TryParse(parts[0], out var address)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var prefix))
        {
            return false;
        }

        address = Unmap(address);
        var maxPrefix = address.AddressFamily == AddressFamily.InterNetwork ? Ipv4Bits : Ipv6Bits;

        // A prefix past the family's width names no range.
        if (prefix > maxPrefix)
        {
            return false;
        }

        network = new IpNetwork(Mask(address.GetAddressBytes(), prefix), prefix);
        return true;
    }

    /// <summary>Tells whether an address lies inside the range.</summary>
    /// <param name="address">The address to test; an IPv4 address mapped into IPv6 is tested as IPv4.</param>
    /// <returns><see langword="true"/> when the address is in the range; an address of the other family never is.</returns>
    public bool Contains(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        var bytes = Unmap(address).GetAddressBytes();
        return bytes.Length == _network.Length && Mask(bytes, PrefixLength).AsSpan().SequenceEqual(_network);
    }

    /// <summary>Writes the range in its canonical form.</summary>
    /// <returns>The network address, a slash and the prefix length.</returns>
    public override string ToString()
    {
        return $"{new IPAddress(_network)}/{PrefixLength}";
    }

    /// <summary>Turns an IPv4 address mapped into IPv6 back into IPv4, so one address has one spelling.</summary>
    /// <param name="address">The address.</param>
    /// <returns>The IPv4 address for a mapped one; the address itself otherwise.</returns>
    internal static IPAddress Unmap(IPAddress address)
    {
        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }

    /// <summary>Clears every bit after the first <paramref name="prefixLength"/>.</summary>
    /// <param name="bytes">The address bytes.</param>
    /// <param name="prefixLength">Number of leading bits to keep.</param>
    /// <returns>A masked copy.</returns>
    private static byte[] Mask(byte[] bytes, int prefixLength)
    {
        var masked = (byte[])bytes.Clone();
        for (var i = 0; i < masked.Length; i++)
        {
            var keep = Math.Clamp(prefixLength - (i * BitsPerByte), 0, BitsPerByte);
            masked[i] &= (byte)(0xFF << (BitsPerByte - keep));
        }

        return masked;
    }
}
