using Innovayse.Hostpanel.Domain.Campaigns.Enums;

namespace Innovayse.Hostpanel.Application.Campaigns.Common;

/// <summary>One row of the admin's list of email campaigns: the campaign without its body and segments, with how its delivery stands.</summary>
/// <param name="Id">Campaign identifier.</param>
/// <param name="Subject">The subject line of the email.</param>
/// <param name="Status">Where the campaign is in its life.</param>
/// <param name="ScheduledAt">When sending is due to start, or <c>null</c> for a draft.</param>
/// <param name="SentAt">When delivery finished, or <c>null</c> until then.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
/// <param name="Stats">How the delivery stands.</param>
public sealed record EmailCampaignListItemDto(
    int Id,
    string Subject,
    CampaignStatus Status,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? SentAt,
    DateTimeOffset CreatedAt,
    EmailCampaignStatsDto Stats);
