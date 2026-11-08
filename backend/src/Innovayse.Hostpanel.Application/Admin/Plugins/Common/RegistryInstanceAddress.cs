using System.Net;

namespace Innovayse.Hostpanel.Application.Admin.Plugins.Common;

/// <summary>
/// Decides whether the configured public address of this installation can be told to the plugin registry. The registry
/// fetches a file from that address, so it must be an https address of a public host: no user name, query or fragment,
/// no name that stays inside a network and no address literal in a private or loopback range. The registry checks the
/// address again; this check only spares a pointless call, and names the cause of the failure on the admin page.
/// </summary>
public static class RegistryInstanceAddress
{
    /// <summary>The longest address the registry keeps.</summary>
    private const int MaxLength = 200;

    /// <summary>Name endings that mean a host inside a network, never a public installation.</summary>
    private static readonly string[] InternalSuffixes = [".local", ".localhost", ".internal", ".lan", ".home", ".corp", ".intranet"];

    /// <summary>Normalises the configured address.</summary>
    /// <param name="configured">The configured public address of the client portal.</param>
    /// <param name="address">The address with a lower-case host and no trailing slash when it is usable; otherwise empty.</param>
    /// <returns><see langword="true"/> when the registry can be told this address.</returns>
    public static bool TryNormalize(string? configured, out string address)
    {
        address = string.Empty;
        if (string.IsNullOrWhiteSpace(configured)
            || configured.Length > MaxLength
            || !Uri.TryCreate(configured.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttps
            || uri.UserInfo.Length > 0
            || uri.Query.Length > 0
            || uri.Fragment.Length > 0
            || uri.HostNameType == UriHostNameType.Unknown)
        {
            return false;
        }

        var host = uri.IdnHost.ToLowerInvariant();
        if (IPAddress.TryParse(host.Trim('[', ']'), out var literal))
        {
            if (IsInternal(literal))
            {
                return false;
            }
        }
        else if (!host.Contains('.') || InternalSuffixes.Any(suffix => host.EndsWith(suffix, StringComparison.Ordinal)))
        {
            return false;
        }

        address = $"https://{uri.Authority.ToLowerInvariant()}{uri.AbsolutePath.TrimEnd('/')}";
        return true;
    }

    /// <summary>Whether an address literal is loopback, unspecified, private, link-local or multicast.</summary>
    /// <param name="ip">The address.</param>
    /// <returns><see langword="true"/> for an address no public registry could reach.</returns>
    private static bool IsInternal(IPAddress ip)
    {
        var address = ip.IsIPv4MappedToIPv6 ? ip.MapToIPv4() : ip;
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        var bytes = address.GetAddressBytes();
        if (bytes.Length == 16)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast || (bytes[0] & 0xFE) == 0xFC;
        }

        return bytes[0] == 0
            || bytes[0] == 10
            || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127)
            || (bytes[0] == 169 && bytes[1] == 254)
            || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
            || (bytes[0] == 192 && bytes[1] == 168)
            || bytes[0] >= 224;
    }
}
