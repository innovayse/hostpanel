namespace Innovayse.Hostpanel.Application.Campaigns.Common;

/// <summary>Builds the link in a campaign mail that opens the client portal's unsubscribe page for one recipient.</summary>
public static class CampaignUnsubscribeLink
{
    /// <summary>The token the preview puts in its sample link, which names no recipient.</summary>
    public const string SampleToken = "sample";

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
}
