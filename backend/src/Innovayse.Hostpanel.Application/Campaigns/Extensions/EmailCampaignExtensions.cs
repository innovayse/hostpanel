using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Domain.Campaigns;
using Innovayse.Hostpanel.Domain.Campaigns.Enums;
using Microsoft.EntityFrameworkCore;

namespace Innovayse.Hostpanel.Application.Campaigns.Extensions;

/// <summary>Mapping and counting helpers for the <see cref="EmailCampaign"/> aggregate, shared by its use cases.</summary>
public static class EmailCampaignExtensions
{
    /// <summary>Maps a campaign, its segments and its delivery stats to the admin DTO.</summary>
    /// <param name="campaign">The campaign to map, with its segments loaded.</param>
    /// <param name="stats">How the campaign's delivery stands.</param>
    /// <returns>The admin DTO.</returns>
    public static EmailCampaignDto ToDto(this EmailCampaign campaign, EmailCampaignStatsDto stats)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(stats);

        return new EmailCampaignDto(
            campaign.Id,
            campaign.Subject,
            campaign.Body,
            campaign.Status,
            campaign.ScheduledAt,
            campaign.SentAt,
            campaign.CreatedAt,
            campaign.CreatedBy,
            campaign.Segments.Select(segment => segment.ToDefinition()).ToList(),
            stats);
    }

    /// <summary>Maps a campaign and its delivery stats to a row of the list.</summary>
    /// <param name="campaign">The campaign to map; its segments need not be loaded.</param>
    /// <param name="stats">How the campaign's delivery stands.</param>
    /// <returns>The list row.</returns>
    public static EmailCampaignListItemDto ToListItem(this EmailCampaign campaign, EmailCampaignStatsDto stats)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(stats);

        return new EmailCampaignListItemDto(
            campaign.Id, campaign.Subject, campaign.Status, campaign.ScheduledAt, campaign.SentAt, campaign.CreatedAt, stats);
    }

    /// <summary>
    /// Counts the recipients of the given campaigns by delivery status in one grouped query, without loading a recipient row.
    /// </summary>
    /// <param name="recipients">The recipients set to count over.</param>
    /// <param name="campaignIds">The campaigns to count for.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The stats of every given campaign; one that has no recipient yet has all-zero stats.</returns>
    public static async Task<IReadOnlyDictionary<int, EmailCampaignStatsDto>> StatsByCampaignAsync(
        this IQueryable<EmailCampaignRecipient> recipients, IReadOnlyCollection<int> campaignIds, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(recipients);
        ArgumentNullException.ThrowIfNull(campaignIds);

        var counts = await recipients
            .Where(r => campaignIds.Contains(r.CampaignId))
            .GroupBy(r => new { r.CampaignId, r.Status })
            .Select(g => new { g.Key.CampaignId, g.Key.Status, Count = g.Count() })
            .ToListAsync(ct);

        var byCampaign = counts.ToLookup(c => c.CampaignId);
        return campaignIds.Distinct().ToDictionary(
            id => id,
            id => ToStats(byCampaign[id].ToDictionary(c => c.Status, c => c.Count)));
    }

    /// <summary>Builds the stats from the per-status counts of one campaign.</summary>
    /// <param name="byStatus">The count of recipients per delivery status; a status with none is absent.</param>
    /// <returns>The stats, with the total over every status.</returns>
    private static EmailCampaignStatsDto ToStats(IReadOnlyDictionary<CampaignRecipientStatus, int> byStatus)
    {
        return new EmailCampaignStatsDto(
            byStatus.Values.Sum(),
            byStatus.GetValueOrDefault(CampaignRecipientStatus.Pending),
            byStatus.GetValueOrDefault(CampaignRecipientStatus.Sent),
            byStatus.GetValueOrDefault(CampaignRecipientStatus.Failed),
            byStatus.GetValueOrDefault(CampaignRecipientStatus.Unsubscribed));
    }
}
