using Innovayse.Hostpanel.Application.Campaigns.Common;

namespace Innovayse.Hostpanel.Application.Campaigns.Queries.CountEmailCampaignAudience;

/// <summary>Query to count the clients a set of campaign segments reaches, so an admin sees the audience before saving or sending.</summary>
/// <param name="Segments">The segments to count; none reaches nobody.</param>
public sealed record CountEmailCampaignAudienceQuery(IReadOnlyList<CampaignSegmentDto> Segments);
