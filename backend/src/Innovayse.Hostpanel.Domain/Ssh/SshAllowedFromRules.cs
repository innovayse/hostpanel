using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace Innovayse.Hostpanel.Domain.Ssh;

/// <summary>
/// The rule for the list of sources an SSH key may be used from, the same rule the server's key agent applies (its
/// <c>from</c> field), so a list the host accepts is one the agent accepts. It is kept here once: the plugin host API, the
/// key entity and the tests all ask it.
/// </summary>
/// <remarks>
/// A source is a plain IPv4 address (dotted decimal, no leading zeros), an IPv6 address (hexadecimal groups only, no embedded
/// IPv4 and no zone), or either with a CIDR prefix whose host bits are zero (<c>198.51.100.0/24</c>; the prefix has no
/// leading zeros). Host names, wildcards and negations are refused, so nothing can reach the authorized key line but an
/// address. A list holds 1 to <see cref="MaxEntries"/> distinct sources (compared without case).
/// </remarks>
public static partial class SshAllowedFromRules
{
    /// <summary>The most sources one key may name.</summary>
    public const int MaxEntries = 16;

    /// <summary>The longest text of an IPv6 address.</summary>
    private const int MaxIpv6Length = 39;

    /// <summary>The prefix length an IPv4 network may have at most.</summary>
    private const int MaxIpv4Prefix = 32;

    /// <summary>The prefix length an IPv6 network may have at most.</summary>
    private const int MaxIpv6Prefix = 128;

    /// <summary>Whether <paramref name="sources"/> is a valid list: 1 to <see cref="MaxEntries"/> valid, distinct sources.</summary>
    /// <param name="sources">The list to check; <see langword="null"/> is not valid.</param>
    /// <returns><see langword="true"/> when every source is valid and none repeats.</returns>
    public static bool IsValidList(IReadOnlyList<string>? sources)
    {
        if (sources is null || sources.Count is 0 or > MaxEntries)
        {
            return false;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (!IsValidSource(source) || !seen.Add(source))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether <paramref name="source"/> is an address or a CIDR network in the shape the agent accepts.</summary>
    /// <param name="source">The text to check.</param>
    /// <returns><see langword="true"/> for a valid source.</returns>
    public static bool IsValidSource(string? source)
    {
        if (string.IsNullOrEmpty(source))
        {
            return false;
        }

        var slash = source.IndexOf('/');
        var address = slash < 0 ? source : source[..slash];
        var prefix = slash < 0 ? null : source[(slash + 1)..];
        if (!TryParseAddress(address, out var ip))
        {
            return false;
        }

        var maxPrefix = ip.AddressFamily == AddressFamily.InterNetwork ? MaxIpv4Prefix : MaxIpv6Prefix;
        if (prefix is null)
        {
            return true;
        }

        return PrefixPattern().IsMatch(prefix)
            && int.TryParse(prefix, out var length)
            && length <= maxPrefix
            && HostBitsAreZero(ip.GetAddressBytes(), length);
    }

    /// <summary>Parses the address part in the agent's shape: plain IPv4, or IPv6 of hexadecimal groups.</summary>
    /// <param name="address">The text before any <c>/</c>.</param>
    /// <param name="ip">The address when valid.</param>
    /// <returns><see langword="true"/> when it is an address in that shape.</returns>
    private static bool TryParseAddress(string address, out IPAddress ip)
    {
        ip = IPAddress.None;
        var family = AddressFamily.InterNetwork;
        if (!Ipv4Pattern().IsMatch(address))
        {
            if (address.Length is < 2 or > MaxIpv6Length
                || address.Count(c => c == ':') < 2
                || !address.All(c => c == ':' || Uri.IsHexDigit(c)))
            {
                return false;
            }

            family = AddressFamily.InterNetworkV6;
        }

        if (!IPAddress.TryParse(address, out var parsed) || parsed.AddressFamily != family)
        {
            return false;
        }

        ip = parsed;
        return true;
    }

    /// <summary>Whether every bit of the address past the prefix is zero.</summary>
    /// <param name="bytes">The address bytes.</param>
    /// <param name="prefix">The prefix length in bits.</param>
    /// <returns><see langword="true"/> when the address is the network's base address.</returns>
    private static bool HostBitsAreZero(byte[] bytes, int prefix)
    {
        for (var bit = prefix; bit < bytes.Length * 8; bit++)
        {
            if ((bytes[bit / 8] & (0x80 >> (bit % 8))) != 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Dotted decimal without leading zeros, each part 0 to 255.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"\A(?:(?:25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|1[0-9]{2}|[1-9]?[0-9])\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Ipv4Pattern();

    /// <summary>A prefix length: digits, no leading zero.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"\A(?:0|[1-9][0-9]{0,2})\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PrefixPattern();
}
