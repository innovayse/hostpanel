namespace Innovayse.Hostpanel.Infrastructure.Integrations.CPanel.Whm;

/// <summary>
/// What WHM may be sent and what it may send back, decided before a call is made and before an
/// answer is trusted.
/// </summary>
/// <remarks>
/// WHM reads every argument as text and answers a malformed one with a refusal that is
/// indistinguishable from any other, so a username or a domain that can never be valid is stopped
/// here instead. A blank value is never a valid one: it would make <c>listaccts</c> match every
/// account or <c>removeacct</c> act on nothing the caller named.
/// </remarks>
public static class WhmInputRules
{
    /// <summary>The longest username WHM creates an account under.</summary>
    public const int MaxNewUsernameLength = 16;

    /// <summary>The longest account reference this client sends for an account that already exists.</summary>
    public const int MaxReferenceLength = 32;

    /// <summary>The longest domain name, without a trailing dot.</summary>
    public const int MaxDomainLength = 253;

    /// <summary>The longest label of a domain name.</summary>
    public const int MaxLabelLength = 63;

    /// <summary>
    /// Whether a username may be given to a new cPanel account: lowercase letters and digits only,
    /// starting with a letter, at most <see cref="MaxNewUsernameLength"/> characters.
    /// </summary>
    /// <param name="value">The username.</param>
    /// <returns><see langword="true"/> when WHM can create an account under it.</returns>
    public static bool IsNewUsername(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxNewUsernameLength || !char.IsAsciiLetterLower(value[0]))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether a value may name an account that already exists. Looser than <see cref="IsNewUsername"/>
    /// on purpose: an account made by an older version, another tool or an import may carry an
    /// underscore, a hyphen or a capital, and it must stay suspendable and removable. Still no
    /// whitespace, control character or separator, and never blank.
    /// </summary>
    /// <param name="value">The account's username as stored.</param>
    /// <returns><see langword="true"/> when it is safe to send as a username argument.</returns>
    public static bool IsAccountReference(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxReferenceLength || !char.IsAsciiLetterOrDigit(value[0]))
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-' or '.'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Whether a value is a domain name WHM can host: ASCII labels of letters, digits and hyphens
    /// (an internationalised name is sent in its punycode form), at least two labels, and a last
    /// label that is not all digits so an IP address is not taken for a domain.
    /// </summary>
    /// <param name="value">The domain.</param>
    /// <returns><see langword="true"/> when the value is a well formed domain name.</returns>
    public static bool IsDomain(string? value)
    {
        if (string.IsNullOrEmpty(value) || value.Length > MaxDomainLength)
        {
            return false;
        }

        var labels = value.Split('.');
        if (labels.Length < 2 || labels[^1].All(char.IsAsciiDigit))
        {
            return false;
        }

        return labels.All(IsLabel);
    }

    /// <summary>
    /// Reads a single sign-on URL WHM returned and answers it only when this server may hand it
    /// out: absolute, <c>https</c>, no user information, a host that is one of the server's own
    /// names, and the port the account owner's cPanel is served on. Hosts compare case
    /// insensitively and in their punycode form, so a lookalike written in another script or case
    /// never matches; a host with a trailing dot is a different name and is refused.
    /// </summary>
    /// <param name="url">The URL WHM returned.</param>
    /// <param name="panelUrl">
    /// The cPanel address an account owner signs in at, e.g. <c>https://host:2083</c>. Its port is
    /// the only port a session URL may carry (an absent port means 443). A blank or unreadable
    /// value accepts nothing, so a server with no known cPanel port never passes a URL on.
    /// </param>
    /// <param name="ownHosts">
    /// The server's own names: the WHM address, and its IP address or host name. A value may be a
    /// bare host or a URL; blank entries are ignored.
    /// </param>
    /// <returns>
    /// The parsed absolute URI that was checked, so a caller hands on exactly what was verified and
    /// not the original text; <see langword="null"/> when the URL may not be given to a client.
    /// </returns>
    public static Uri? ParseSessionUrl(string? url, string? panelUrl, params string?[] ownHosts)
    {
        ArgumentNullException.ThrowIfNull(ownHosts);

        if (string.IsNullOrWhiteSpace(url) || url.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)))
        {
            return null;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var session)
            || session.Scheme != Uri.UriSchemeHttps
            || session.UserInfo.Length > 0
            || PortOf(panelUrl) != session.Port)
        {
            return null;
        }

        var onServer = ownHosts
            .Select(HostOf)
            .Any(host => host is not null && string.Equals(host, session.IdnHost, StringComparison.OrdinalIgnoreCase));
        return onServer ? session : null;
    }

    /// <summary>Reads the port out of the cPanel address, with the scheme's default when it names none.</summary>
    /// <param name="panelUrl">The cPanel address, e.g. <c>https://host:2083</c>.</param>
    /// <returns>The port, or -1 when the value is not an absolute https address, which equals no port a URL carries.</returns>
    private static int PortOf(string? panelUrl)
    {
        return Uri.TryCreate(panelUrl, UriKind.Absolute, out var parsed) && parsed.Scheme == Uri.UriSchemeHttps ? parsed.Port : -1;
    }

    /// <summary>Reads the host out of a server address given as a URL or as a bare host or IP address.</summary>
    /// <param name="address">The address.</param>
    /// <returns>The host in its punycode form, or null when the value holds no host.</returns>
    private static string? HostOf(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        string candidate;
        if (address.Contains("://", StringComparison.Ordinal))
        {
            candidate = address;
        }
        else if (address.Contains(':', StringComparison.Ordinal) && !address.StartsWith('['))
        {
            // A bare IPv6 address needs its brackets to be read as a host.
            candidate = $"https://[{address}]";
        }
        else
        {
            candidate = $"https://{address}";
        }

        return Uri.TryCreate(candidate, UriKind.Absolute, out var parsed) ? parsed.IdnHost : null;
    }

    /// <summary>Whether a string is one label of a domain name.</summary>
    /// <param name="label">The label.</param>
    /// <returns><see langword="true"/> for 1 to 63 letters, digits and hyphens, not starting or ending with a hyphen.</returns>
    private static bool IsLabel(string label)
    {
        if (label.Length is 0 or > MaxLabelLength || label[0] == '-' || label[^1] == '-')
        {
            return false;
        }

        return label.All(c => char.IsAsciiLetterOrDigit(c) || c == '-');
    }
}
