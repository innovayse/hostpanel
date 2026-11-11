namespace Innovayse.Hostpanel.Application.Campaigns.Common;

/// <summary>
/// The tracking addresses of a campaign mail and the routes behind them, declared once: the mail builder writes the addresses
/// with them and the public controller serves the routes from them, so the two cannot drift apart. The addresses point at the
/// client portal's origin, which is the one public address the deployment's edge routes <c>/api/</c> from to the API.
/// </summary>
public static class CampaignTrackingLinks
{
    /// <summary>The route prefix of the public tracking controller.</summary>
    public const string RoutePrefix = "api/public/campaign-mail";

    /// <summary>The path segment of the open pixel.</summary>
    public const string OpenSegment = "open";

    /// <summary>The path segment of a tracked link.</summary>
    public const string ClickSegment = "click";

    /// <summary>The route template of the open pixel, below <see cref="RoutePrefix"/>.</summary>
    public const string OpenRoute = OpenSegment + "/{token}";

    /// <summary>The route template of a tracked link, below <see cref="RoutePrefix"/>; the index is the link's position in the body.</summary>
    public const string ClickRoute = ClickSegment + "/{token}/{index:int}";

    /// <summary>
    /// Builds the body one recipient's mail carries: every trackable link points at the click endpoint under the recipient's
    /// token, and a one-pixel image that reports the open ends the body.
    /// </summary>
    /// <param name="body">The campaign's HTML body as the admin wrote it.</param>
    /// <param name="portalBaseUrl">The client portal's public address.</param>
    /// <param name="token">The recipient's tracking token.</param>
    /// <returns>The body to mail to this recipient.</returns>
    public static string Track(string body, string portalBaseUrl, string token)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentException.ThrowIfNullOrWhiteSpace(portalBaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var rewritten = CampaignBodyLinks.Rewrite(body, index => ClickUrl(portalBaseUrl, token, index));
        return $"{rewritten}<img src=\"{OpenUrl(portalBaseUrl, token)}\" width=\"1\" height=\"1\" alt=\"\" style=\"display:block;border:0;width:1px;height:1px;\">";
    }

    /// <summary>Builds the address of the open pixel.</summary>
    /// <param name="portalBaseUrl">The client portal's public address.</param>
    /// <param name="token">The recipient's tracking token.</param>
    /// <returns>The absolute address of the pixel.</returns>
    public static string OpenUrl(string portalBaseUrl, string token)
    {
        return $"{Prefix(portalBaseUrl)}/{OpenSegment}/{Uri.EscapeDataString(token)}";
    }

    /// <summary>Builds the address that records a click and redirects to the link at an index of the body.</summary>
    /// <param name="portalBaseUrl">The client portal's public address.</param>
    /// <param name="token">The recipient's tracking token.</param>
    /// <param name="index">The link's position among the body's trackable links.</param>
    /// <returns>The absolute address of the tracked link.</returns>
    public static string ClickUrl(string portalBaseUrl, string token, int index)
    {
        return $"{Prefix(portalBaseUrl)}/{ClickSegment}/{Uri.EscapeDataString(token)}/{index}";
    }

    /// <summary>Joins the portal's address and the tracking route prefix.</summary>
    /// <param name="portalBaseUrl">The client portal's public address.</param>
    /// <returns>The address every tracking route sits below.</returns>
    private static string Prefix(string portalBaseUrl)
    {
        return $"{portalBaseUrl.TrimEnd('/')}/{RoutePrefix}";
    }
}
