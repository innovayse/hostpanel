using System.Diagnostics.CodeAnalysis;
using Hostpanel.Sdk.Plugins.Manifest;

namespace Innovayse.Hostpanel.Application.Plugins.Common;

/// <summary>
/// Decides whether the address a plugin's callback redirects the payer to is one the host will put in a <c>Location</c>
/// header, and writes the address it will send. The SDK refuses the obvious mistakes when the reply is built, and says
/// the host checks again, because a reply can be built another way; these rules are the host's own and are narrower than
/// the SDK's on purpose.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two shapes pass.</b> A host relative path: one leading slash, never two. An absolute <c>https</c> address of an
/// origin on the allowed list (see <see cref="AllowedOrigins"/>), with no user info and the default port. Anything else is
/// refused, so a plugin that builds the location from the request (<c>?next=https://evil.example</c>) cannot send a payer
/// to a host nobody named.
/// </para>
/// <para>
/// <b>Both shapes share the path rules.</b> No control character (a line break, a tab, a null, DEL or a C1 control), no
/// white space, no backslash, no non-ASCII character (a header value must be ASCII; a plugin percent-encodes), no fragment,
/// and at most <see cref="MaxLength"/> characters. A path segment, read after one round of percent decoding, is never a dot
/// segment and never holds a slash or a backslash, so <c>%2f</c>, <c>%5c</c> and <c>%2e%2e</c> cannot smuggle what the
/// plain text would be refused for. The first segment is never <c>api</c> in any case, so a payer is not sent to the API
/// itself. The query is passed on as it came: a payment result page needs it, and it carries a payment token.
/// </para>
/// </remarks>
public static class PluginRedirectLocation
{
    /// <summary>The longest address the host passes on, in characters, within what proxies accept in one header.</summary>
    public const int MaxLength = 2048;

    /// <summary>The scheme of an absolute address.</summary>
    private const string SecureScheme = "https";

    /// <summary>The first path segment that is the API's own and is never a redirect target, compared without case.</summary>
    private const string ApiSegment = "api";

    /// <summary>The highest code point of ASCII.</summary>
    private const char MaxAscii = '\u007F';

    /// <summary>Lists the origins an absolute redirect may go to: the client portal's and the manifest's <c>externalOrigins</c>.</summary>
    /// <param name="manifest">The plugin's manifest; its pure <c>https</c> origins count, as for a callback page.</param>
    /// <param name="portalBaseUrl">The client portal's base address (<c>ClientBaseUrl</c>); it counts only when it is <c>https</c>.</param>
    /// <returns>The distinct canonical origins, in a stable order; empty when neither names one.</returns>
    public static IReadOnlyList<string> AllowedOrigins(PluginManifest manifest, string portalBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        var origins = PluginPageOrigins.From(manifest).ToList();
        if (Uri.TryCreate(portalBaseUrl, UriKind.Absolute, out var portal)
            && PluginFrameOrigin.FromHttps(portal.GetLeftPart(UriPartial.Authority)) is { } own
            && !origins.Contains(own, StringComparer.Ordinal))
        {
            origins.Add(own);
        }

        return origins;
    }

    /// <summary>Checks an address and writes the one the host sends.</summary>
    /// <param name="location">The address as the plugin gave it.</param>
    /// <param name="allowedOrigins">The origins an absolute address may name, from <see cref="AllowedOrigins"/>.</param>
    /// <param name="safe">The address to put in the header: the relative address as it came, or the allowed origin in its canonical form followed by the path and query; <see langword="null"/> when refused.</param>
    /// <returns><see langword="true"/> when the address passes every rule.</returns>
    public static bool TryNormalize(string? location, IReadOnlyCollection<string> allowedOrigins, [NotNullWhen(true)] out string? safe)
    {
        ArgumentNullException.ThrowIfNull(allowedOrigins);

        safe = null;
        if (string.IsNullOrEmpty(location)
            || location.Length > MaxLength
            || location.Any(c => char.IsControl(c) || char.IsWhiteSpace(c) || c == '\\' || c > MaxAscii || c == '#'))
        {
            return false;
        }

        if (location[0] == '/')
        {
            safe = HasSafePath(location) ? location : null;
            return safe is not null;
        }

        // Only an absolute https address of an allowed origin, with no port and no user info, is left.
        var prefix = SecureScheme + Uri.SchemeDelimiter;
        if (!location.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var rest = location[prefix.Length..];
        var authorityEnd = rest.IndexOfAny(['/', '?']);
        var authority = authorityEnd < 0 ? rest : rest[..authorityEnd];
        var remainder = authorityEnd < 0 ? string.Empty : rest[authorityEnd..];
        if (authority.Contains(':')
            || authority.Contains('@')
            || PluginFrameOrigin.FromHttps(prefix + authority) is not { } origin
            || !allowedOrigins.Contains(origin, StringComparer.Ordinal)
            || (remainder.Length > 0 && !HasSafePath(remainder)))
        {
            return false;
        }

        safe = origin + remainder;
        return true;
    }

    /// <summary>
    /// Checks the path of an address that starts at the first slash or at the query: not <c>//</c>, no segment that is a dot
    /// segment or holds an encoded slash or backslash, and never the API.
    /// </summary>
    /// <param name="pathAndQuery">The address from its first slash (or its <c>?</c>) on, with no fragment.</param>
    /// <returns><see langword="true"/> when the path is one the host will send a payer to.</returns>
    private static bool HasSafePath(string pathAndQuery)
    {
        // "//host" is a network path reference and "/\host" is read as one by a browser, so the second character is checked first.
        if (pathAndQuery.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        var queryStart = pathAndQuery.IndexOf('?');
        var path = queryStart < 0 ? pathAndQuery : pathAndQuery[..queryStart];
        var segments = path.Split('/');

        // The first element is the empty text before the leading slash (or the whole empty path of a bare query).
        for (var i = 1; i < segments.Length; i++)
        {
            var decoded = Uri.UnescapeDataString(segments[i]);
            if (decoded is "." or ".."
                || decoded.Any(c => c is '/' or '\\' || char.IsControl(c)))
            {
                return false;
            }
        }

        return segments.Length < 2 || !string.Equals(Uri.UnescapeDataString(segments[1]), ApiSegment, StringComparison.OrdinalIgnoreCase);
    }
}
