using Innovayse.Hostpanel.Domain.Campaigns.Enums;

namespace Innovayse.Hostpanel.Domain.Campaigns;

/// <summary>
/// One segment of a campaign as a caller writes it and as the campaign reads back: the shape the admin API sends and answers with,
/// and what the audience is matched by. The campaign checks the pair before it builds a segment from it.
/// </summary>
/// <param name="FilterType">The kind of rule the segment uses.</param>
/// <param name="FilterValue">The value the rule needs, or <see langword="null"/> for a rule that takes none.</param>
public sealed record CampaignSegmentDefinition(CampaignFilterType FilterType, string? FilterValue);
