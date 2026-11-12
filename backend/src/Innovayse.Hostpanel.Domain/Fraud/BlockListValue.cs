using System.Net;
using Innovayse.Hostpanel.Domain.Fraud.Enums;

namespace Innovayse.Hostpanel.Domain.Fraud;

/// <summary>The one place a block list value is checked against its type and put in the spelling it is stored and compared in.</summary>
public static class BlockListValue
{
    /// <summary>Longest value any type holds; the column is sized to it.</summary>
    public const int MaxLength = 255;

    /// <summary>Length of an ISO 3166-1 alpha-2 country code.</summary>
    private const int CountryCodeLength = 2;

    /// <summary>Longest label of a domain name.</summary>
    private const int MaxLabelLength = 63;

    /// <summary>Checks a value and puts it in its stored spelling.</summary>
    /// <param name="type">What the value names.</param>
    /// <param name="value">The value as typed.</param>
    /// <param name="normalised">The stored spelling: a canonical IP or range, a lower case domain without a leading at sign, an upper case country code.</param>
    /// <returns><see langword="true"/> when the value fits the type.</returns>
    public static bool TryNormalise(BlockListType type, string? value, out string normalised)
    {
        normalised = string.Empty;
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 0 || text.Length > MaxLength)
        {
            return false;
        }

        switch (type)
        {
            case BlockListType.Ip:
                if (!IPAddress.TryParse(text, out var address))
                {
                    return false;
                }

                normalised = IpNetwork.Unmap(address).ToString();
                return true;
            case BlockListType.Cidr:
                if (!IpNetwork.TryParse(text, out var network))
                {
                    return false;
                }

                normalised = network.ToString();
                return true;
            case BlockListType.EmailDomain:
                text = text.TrimStart('@').ToLowerInvariant();
                normalised = text;
                return IsDomain(text);
            case BlockListType.Country:
                text = text.ToUpperInvariant();
                normalised = text;
                return text.Length == CountryCodeLength && text.All(c => c is >= 'A' and <= 'Z');
            default:
                return false;
        }
    }

    /// <summary>Tells whether a lower case text is a domain name of at least two labels.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> for letters, digits and inner hyphens in dot separated labels.</returns>
    private static bool IsDomain(string text)
    {
        var labels = text.Split('.');
        return labels.Length >= 2 && labels.All(IsLabel);
    }

    /// <summary>Tells whether a text is one domain label.</summary>
    /// <param name="label">The label.</param>
    /// <returns><see langword="true"/> for 1 to 63 letters, digits or hyphens that neither starts nor ends with a hyphen.</returns>
    private static bool IsLabel(string label)
    {
        return label.Length is >= 1 and <= MaxLabelLength
            && label[0] != '-'
            && label[^1] != '-'
            && label.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '-');
    }
}
