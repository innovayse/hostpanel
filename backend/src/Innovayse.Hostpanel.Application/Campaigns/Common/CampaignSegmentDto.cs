using Innovayse.Hostpanel.Domain.Campaigns.Enums;

namespace Innovayse.Hostpanel.Application.Campaigns.Common;

/// <summary>
/// One segment of a campaign as the admin API sends and answers with it, and as the audience is matched by. The campaign
/// itself takes the pair as a domain <c>CampaignSegmentDefinition</c> and checks it there.
/// </summary>
/// <param name="FilterType">The kind of rule the segment uses.</param>
/// <param name="FilterValue">The value the rule needs, or <see langword="null"/> for a rule that takes none.</param>
public sealed record CampaignSegmentDto(CampaignFilterType FilterType, string? FilterValue);
