namespace Innovayse.Hostpanel.Application.Campaigns.Common;

/// <summary>
/// Builds the two addresses a campaign mail carries for one recipient: the client portal's unsubscribe page, which the reader
/// opens from the footer, and the one-click endpoint (RFC 8058) a mail client posts to from its own unsubscribe button.
/// </summary>
public static class CampaignUnsubscribeLink
{
    /// <summary>The token the preview puts in its sample link, which names no recipient.</summary>
    public const string SampleToken = "sample";

    /// <summary>
    /// The path of the one-click endpoint on the public address. It is under <c>/api/</c>, which the edge sends to the API on the
    /// same host as the portal, so the mail client's POST reaches the API without passing the portal's own server.
    /// </summary>
    public const string OneClickPath = "/api/public/unsubscribe/one-click";

    /// <summary>Builds the absolute unsubscribe link.</summary>
    /// <param name="portalBaseUrl">The client portal's public address.</param>
    /// <param name="token">The recipient's unsubscribe token.</param>
    /// <returns>The link the page reads the token out of.</returns>
    public static string For(string portalBaseUrl, string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portalBaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return $"{portalBaseUrl.TrimEnd('/')}/unsubscribe?token={Uri.EscapeDataString(token)}";
    }

    /// <summary>Builds the absolute address of the one-click endpoint, which unsubscribes on a bare POST.</summary>
    /// <param name="publicBaseUrl">The public address the API is reachable on, the same host as the client portal.</param>
    /// <param name="token">The recipient's unsubscribe token.</param>
    /// <returns>The address the mail's <c>List-Unsubscribe</c> header names.</returns>
    public static string OneClickFor(string publicBaseUrl, string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(publicBaseUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        return $"{publicBaseUrl.TrimEnd('/')}{OneClickPath}?token={Uri.EscapeDataString(token)}";
    }
}
