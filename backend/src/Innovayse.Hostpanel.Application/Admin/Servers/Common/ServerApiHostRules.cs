using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Infrastructure.Enums;

namespace Innovayse.Hostpanel.Application.Admin.Servers.Common;

/// <summary>
/// The one definition of what a server's API address may be: a host name or an IP address, nothing else. Create and update
/// both validate through it, so the two can never disagree. A blank address is no address (the API is dialled at the host
/// name), so every rule lets it pass.
/// </summary>
public static partial class ServerApiHostRules
{
    /// <summary>Longest an API address may be: the longest a host name may be.</summary>
    public const int MaxLength = 253;

    /// <summary>
    /// Tells whether an address is a host name or an IP address: dot separated labels of letters, digits and hyphens (a label
    /// neither starts nor ends with a hyphen), or an IPv4 or IPv6 literal. A scheme, a path, a port, credentials, a query, a
    /// fragment, brackets, a zone id and any space inside make it something else and are refused. Spaces around the address are
    /// ignored, as the server entity trims what it stores.
    /// </summary>
    /// <param name="value">The address as typed, or <see langword="null"/> or blank for none.</param>
    /// <returns><see langword="true"/> when the address is blank or a host name or an IP address.</returns>
    public static bool IsWellFormed(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var address = value.Trim();

        // Something shaped like an IP literal must be one: 256.1.1.1 is not a host name either.
        if (Ipv4Pattern().IsMatch(address) || address.Contains(':', StringComparison.Ordinal))
        {
            return TryParseLiteral(address, out _);
        }

        return address.Length <= MaxLength && HostNamePattern().IsMatch(address);
    }

    /// <summary>
    /// Tells whether an address is one the host may dial. An IP address the outbound policy never allows (loopback, link-local,
    /// the cloud metadata endpoints, unspecified, multicast) is refused here, at the form, with the sentence that says why; a
    /// host name passes, because only what it resolves to decides, and the guarded connector checks that on every call.
    /// </summary>
    /// <param name="value">The address as typed, or <see langword="null"/> or blank for none.</param>
    /// <returns><see langword="false"/> only for an IP address that is never dialled.</returns>
    public static bool IsDialable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !TryParseLiteral(value.Trim(), out var address))
        {
            return true;
        }

        return OutboundAddressPolicy.Classify(address) != OutboundAddressClass.Refused;
    }

    /// <summary>Reads an IPv4 literal (four decimal parts) or an IPv6 literal without brackets or a zone id.</summary>
    /// <param name="value">The text to read.</param>
    /// <param name="address">The address, when the text is a literal.</param>
    /// <returns><see langword="true"/> when the text is an IP literal.</returns>
    private static bool TryParseLiteral(string value, out IPAddress address)
    {
        address = IPAddress.None;
        var looksLikeV4 = Ipv4Pattern().IsMatch(value);
        var looksLikeV6 = value.Contains(':', StringComparison.Ordinal) && value.IndexOfAny(['%', '[', ']']) < 0;
        if ((!looksLikeV4 && !looksLikeV6) || !IPAddress.TryParse(value, out var parsed))
        {
            return false;
        }

        if ((looksLikeV4 && parsed.AddressFamily != AddressFamily.InterNetwork) || (looksLikeV6 && parsed.AddressFamily != AddressFamily.InterNetworkV6))
        {
            return false;
        }

        address = parsed;
        return true;
    }

    /// <summary>Four decimal parts separated by dots, the shape of an IPv4 literal.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex Ipv4Pattern();

    /// <summary>One or more dot separated labels of 1 to 63 letters, digits and hyphens that neither start nor end with a hyphen.</summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"^[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?(\.[A-Za-z0-9]([A-Za-z0-9-]{0,61}[A-Za-z0-9])?)*$", RegexOptions.CultureInvariant)]
    private static partial Regex HostNamePattern();
}
