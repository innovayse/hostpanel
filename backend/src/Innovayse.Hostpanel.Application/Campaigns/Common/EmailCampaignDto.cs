using Innovayse.Hostpanel.Domain.Campaigns.Enums;

namespace Innovayse.Hostpanel.Application.Campaigns.Common;

/// <summary>An email campaign as the admin panel edits and reads it: content, segments and how its delivery stands.</summary>
/// <param name="Id">Campaign identifier.</param>
/// <param name="Subject">The subject line of the email.</param>
/// <param name="Body">The HTML body of the email.</param>
/// <param name="Status">Where the campaign is in its life.</param>
/// <param name="ScheduledAt">When sending is due to start, or <c>null</c> for a draft.</param>
/// <param name="SentAt">When delivery finished, or <c>null</c> until then.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
/// <param name="CreatedBy">The identity subject of the admin who wrote the campaign.</param>
/// <param name="Segments">The rules that pick the recipients.</param>
/// <param name="Stats">How the delivery stands.</param>
public sealed record EmailCampaignDto(
    int Id,
    string Subject,
    string Body,
    CampaignStatus Status,
    DateTimeOffset? ScheduledAt,
    DateTimeOffset? SentAt,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    IReadOnlyList<CampaignSegmentDto> Segments,
    EmailCampaignStatsDto Stats);
