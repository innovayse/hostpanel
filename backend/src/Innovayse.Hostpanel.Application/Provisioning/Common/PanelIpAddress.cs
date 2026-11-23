using System.Net;

namespace Innovayse.Hostpanel.Application.Provisioning.Common;

/// <summary>
/// The one check an address a panel reports must pass before it is kept on a service: an IPv4 or IPv6 address in text form that fits
/// the stored column. A panel's answer is only a claim, so anything else is left out rather than stored.
/// </summary>
public static class PanelIpAddress
{
    /// <summary>The longest address that fits the stored column, an IPv6 address with an embedded IPv4 tail.</summary>
    private const int MaxLength = 45;

    /// <summary>Reads an address as the platform stores it.</summary>
    /// <param name="value">The address a panel reported, or what the service holds.</param>
    /// <returns>
    /// The address in its canonical text form, or <see langword="null"/> when <paramref name="value"/> is empty, too long or not an
    /// IPv4 or IPv6 address (a bare number such as <c>1</c> is not one).
    /// </returns>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxLength)
        {
            return null;
        }

        var text = value.Trim();
        var hasAddressSeparator = text.Contains('.', StringComparison.Ordinal) || text.Contains(':', StringComparison.Ordinal);
        return hasAddressSeparator && IPAddress.TryParse(text, out var address) ? address.ToString() : null;
    }
}
