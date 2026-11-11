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
            campaign.Segments.ToDtos(),
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
    /// Counts the recipients of the given campaigns by delivery status, and sums how the readers responded, in one grouped query,
    /// without loading a recipient row.
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

        var rows = await recipients
            .Where(r => campaignIds.Contains(r.CampaignId))
            .GroupBy(r => r.CampaignId)
            .Select(g => new
            {
                CampaignId = g.Key,
                Total = g.Count(),
                Pending = g.Count(r => r.Status == CampaignRecipientStatus.Pending),
                Sent = g.Count(r => r.Status == CampaignRecipientStatus.Sent),
                Failed = g.Count(r => r.Status == CampaignRecipientStatus.Failed),
                Unsubscribed = g.Count(r => r.Status == CampaignRecipientStatus.Unsubscribed),

                // A reader who unsubscribed after the mail arrived was delivered to, so the rates are taken over every
                // recipient that has a delivery moment, not over the Sent status alone.
                Delivered = g.Count(r => r.SentAt != null),
                Opened = g.Sum(r => r.OpenCount),

                // Only a delivered mail counts as a reader, so a rate over the delivered mails cannot pass 1: a mail can be
                // opened without a delivery moment, since the sender saves the delivery after the mail has left.
                UniqueOpened = g.Count(r => r.SentAt != null && r.FirstOpenedAt != null),
                Clicked = g.Sum(r => r.ClickCount),
                UniqueClicked = g.Count(r => r.SentAt != null && r.FirstClickedAt != null),
            })
            .ToListAsync(ct);

        var byCampaign = rows.ToDictionary(row => row.CampaignId);
        return campaignIds.Distinct().ToDictionary(
            id => id,
            id => byCampaign.TryGetValue(id, out var row)
                ? new EmailCampaignStatsDto(
                    row.Total,
                    row.Pending,
                    row.Sent,
                    row.Failed,
                    row.Unsubscribed,
                    row.Opened,
                    row.UniqueOpened,
                    row.Clicked,
                    row.UniqueClicked,
                    Rate(row.UniqueOpened, row.Delivered),
                    Rate(row.UniqueClicked, row.Delivered))
                : new EmailCampaignStatsDto(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
    }

    /// <summary>Turns a count into a share of a whole, rounded to four decimals.</summary>
    /// <param name="part">The recipients that did the thing.</param>
    /// <param name="whole">The recipients that could have.</param>
    /// <returns>The share from 0 to 1; 0 when there is no whole.</returns>
    private static double Rate(int part, int whole)
    {
        return whole == 0 ? 0 : Math.Round((double)part / whole, 4);
    }
}
