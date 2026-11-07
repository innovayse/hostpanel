using System.Globalization;
using System.Text.RegularExpressions;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// Turns what a manifest or a service row says into the canonical origin a browser reports (<c>URL.origin</c>), or nothing.
/// The text ends up in a response header and in the frame check of the panel, and both of its sources are untrusted: a
/// domain is what a client typed at an order, and <c>externalOrigins</c> is not checked at install because it also serves
/// links. So only a plain name passes; anything that could carry a header or policy delimiter is dropped.
/// </summary>
public static partial class PluginFrameOrigin
{
    /// <summary>The scheme of every origin a production host may frame.</summary>
    private const string SecureScheme = "https";

    /// <summary>The scheme only a loopback origin on a developer machine may use.</summary>
    private const string PlainScheme = "http";

    /// <summary>The prefix of the conventional second name of a site, also framed for a domain.</summary>
    private const string WwwPrefix = "www.";

    /// <summary>The loopback name a browser treats as secure, itself and every name under it.</summary>
    private const string LoopbackName = "localhost";

    /// <summary>The loopback addresses, as <see cref="Uri.Host"/> writes them.</summary>
    private static readonly string[] LoopbackAddresses = ["127.0.0.1", "[::1]"];

    /// <summary>Punycode for an international domain, strict about what it accepts.</summary>
    private static readonly IdnMapping Idn = new() { UseStd3AsciiRules = true };

    /// <summary>
    /// Matches a DNS name of at least two labels whose last label starts with a letter (so no IP address), in lower case;
    /// nothing after it, not even a newline.
    /// </summary>
    /// <returns>The compiled pattern.</returns>
    [GeneratedRegex(@"\A([a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]([a-z0-9-]{0,61}[a-z0-9])?\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DnsNamePattern();

    /// <summary>Reads a manifest entry that must be a pure <c>https</c> origin.</summary>
    /// <param name="text">The entry as written.</param>
    /// <returns>The canonical origin, or <see langword="null"/> when it is anything else.</returns>
    public static string? FromHttps(string text)
    {
        var uri = ParsePure(text, SecureScheme);
        return uri is not null && DnsNamePattern().IsMatch(uri.IdnHost) ? Canonical(uri, uri.IdnHost) : null;
    }

    /// <summary>Reads a configured origin that must be <c>http</c> on a loopback host.</summary>
    /// <param name="text">The entry as written.</param>
    /// <returns>The canonical origin, or <see langword="null"/> when it is anything else.</returns>
    public static string? FromLoopbackHttp(string text)
    {
        var uri = ParsePure(text, PlainScheme);
        return uri is not null && IsLoopback(uri.Host) ? Canonical(uri, uri.Host) : null;
    }

    /// <summary>The origins of a site on a domain: the domain itself and its <c>www</c> name.</summary>
    /// <param name="domain">The domain as stored on a service.</param>
    /// <returns>The origins; empty when the value is not a plain DNS name (an IP, one label, a port, an odd character).</returns>
    public static IReadOnlyList<string> FromDomain(string domain)
    {
        if (!TryToAscii(domain, out var name) || !DnsNamePattern().IsMatch(name))
        {
            return [];
        }

        // A stored "www.example.com" still frames the bare name: a site answers on both or redirects between them.
        var bare = name.StartsWith(WwwPrefix, StringComparison.Ordinal) ? name[WwwPrefix.Length..] : name;
        return DnsNamePattern().IsMatch(bare)
            ? [$"{SecureScheme}{Uri.SchemeDelimiter}{bare}", $"{SecureScheme}{Uri.SchemeDelimiter}{WwwPrefix}{bare}"]
            : [$"{SecureScheme}{Uri.SchemeDelimiter}{name}"];
    }

    /// <summary>Parses text that is exactly an origin of one scheme: no user info, path, query or fragment.</summary>
    /// <param name="text">The text.</param>
    /// <param name="scheme">The only scheme accepted, compared in the text itself, so no spelling slips through a parser.</param>
    /// <returns>The address, or <see langword="null"/>.</returns>
    private static Uri? ParsePure(string text, string scheme)
    {
        var prefix = scheme + Uri.SchemeDelimiter;
        if (!text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            || text.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c == '\\')
            || text[prefix.Length..].Contains('/')
            || !Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.UserInfo.Length == 0 && uri.Query.Length == 0 && uri.Fragment.Length == 0 ? uri : null;
    }

    /// <summary>Writes an origin the way a browser does: lower case, with the default port dropped.</summary>
    /// <param name="uri">The parsed address.</param>
    /// <param name="host">The host to write, already in its ASCII form.</param>
    /// <returns>The origin.</returns>
    private static string Canonical(Uri uri, string host)
    {
        var origin = $"{uri.Scheme}{Uri.SchemeDelimiter}{host.ToLowerInvariant()}";
        return uri.IsDefaultPort ? origin : $"{origin}:{uri.Port.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>Tells whether a host is a loopback name or address, the only plain http a secure panel may frame.</summary>
    /// <param name="host">The host, as <see cref="Uri.Host"/> writes it.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsLoopback(string host)
    {
        return string.Equals(host, LoopbackName, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + LoopbackName, StringComparison.OrdinalIgnoreCase)
            || LoopbackAddresses.Contains(host, StringComparer.Ordinal);
    }

    /// <summary>Turns a domain into its lower-case ASCII name.</summary>
    /// <param name="domain">The domain as stored.</param>
    /// <param name="name">The name, or empty when it cannot be converted.</param>
    /// <returns><see langword="true"/> when it was converted.</returns>
    private static bool TryToAscii(string domain, out string name)
    {
        try
        {
            name = Idn.GetAscii(domain.Trim()).ToLowerInvariant();
            return true;
        }
        catch (ArgumentException)
        {
            // Not a name IDNA can encode, which is an ordinary answer for a value a client typed.
            name = string.Empty;
            return false;
        }
    }
}
