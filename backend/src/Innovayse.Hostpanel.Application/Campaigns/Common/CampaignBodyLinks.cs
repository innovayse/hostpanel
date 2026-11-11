using System.Net;
using System.Text.RegularExpressions;

namespace Innovayse.Hostpanel.Application.Campaigns.Common;

/// <summary>
/// The one place that reads the links out of a campaign's HTML body. The mail is built by <see cref="Rewrite"/> and the click
/// is answered from <see cref="Extract"/>, and both walk the same anchors in the same order, so the position a rewritten link
/// carries always names the link it replaced. Only absolute http and https links count: anything else (a mail address, a
/// phone number, an in-page anchor, a relative path, a script) is neither rewritten nor ever a redirect target.
/// </summary>
public static class CampaignBodyLinks
{
    /// <summary>Longest link the click endpoint will redirect to; a longer one is left as the admin wrote it.</summary>
    public const int MaxUrlLength = 2048;

    /// <summary>
    /// Finds an anchor tag and the quoted value of its <c>href</c>. The non-backtracking engine matches in time linear in the
    /// body, so an adversarial body of many unclosed tags cannot stall the sender.
    /// </summary>
    private static readonly Regex Anchor = new(
        @"<a\b[^>]*?\shref\s*=\s*(?:""(?<url>[^""]*)""|'(?<url>[^']*)')",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    /// <summary>Lists the links of a body that are tracked, in the order they appear.</summary>
    /// <param name="body">The campaign's HTML body.</param>
    /// <returns>The absolute http or https address of each trackable link; the position in the list is the link's index.</returns>
    public static IReadOnlyList<string> Extract(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var targets = new List<string>();
        foreach (Match match in Anchor.Matches(body))
        {
            if (TryRead(match, out var target))
            {
                targets.Add(target);
            }
        }

        return targets;
    }

    /// <summary>Replaces the address of every trackable link of a body, leaving the rest of the markup as it was.</summary>
    /// <param name="body">The campaign's HTML body.</param>
    /// <param name="trackedUrlFor">Builds the replacement address from the link's index, the position <see cref="Extract"/> reports it at.</param>
    /// <returns>The body with each trackable link pointing at its replacement.</returns>
    public static string Rewrite(string body, Func<int, string> trackedUrlFor)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(trackedUrlFor);

        var index = 0;
        return Anchor.Replace(body, match =>
        {
            if (!TryRead(match, out _))
            {
                return match.Value;
            }

            var url = match.Groups["url"];
            var start = url.Index - match.Index;
            return string.Concat(match.Value.AsSpan(0, start), trackedUrlFor(index++), match.Value.AsSpan(start + url.Length));
        });
    }

    /// <summary>Reads the address of one anchor and decides whether it is trackable.</summary>
    /// <param name="match">The anchor match.</param>
    /// <param name="target">The normalised absolute address when the link is trackable; empty otherwise.</param>
    /// <returns><see langword="true"/> when the link is an absolute http or https address of a sensible length with no control characters.</returns>
    private static bool TryRead(Match match, out string target)
    {
        // The attribute holds HTML text: &amp; in a query string is a plain &, which is what the browser would have followed.
        var decoded = WebUtility.HtmlDecode(match.Groups["url"].Value).Trim();
        if (decoded.Length is 0 or > MaxUrlLength
            || decoded.Any(char.IsControl)
            || !Uri.TryCreate(decoded, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            target = string.Empty;
            return false;
        }

        target = uri.AbsoluteUri;
        return true;
    }
}
