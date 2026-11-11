namespace Innovayse.Hostpanel.Application.Campaigns.Common;

/// <summary>Bounds the campaign use cases enforce on what a caller sends, so one request cannot be made arbitrarily large.</summary>
public static class CampaignLimits
{
    /// <summary>Most segments one campaign may carry.</summary>
    public const int MaxSegments = 50;
}
