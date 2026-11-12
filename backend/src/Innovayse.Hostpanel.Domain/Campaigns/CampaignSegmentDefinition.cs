using Innovayse.Hostpanel.Domain.Campaigns.Enums;

namespace Innovayse.Hostpanel.Domain.Campaigns;

/// <summary>
/// One segment of a campaign as the aggregate takes it: the pair <see cref="EmailCampaign"/> checks before it builds an
/// <see cref="EmailCampaignSegment"/> from it. It is the domain's input only; the shape the admin API sends and answers with is
/// <c>CampaignSegmentDto</c> in the Application layer.
/// </summary>
/// <param name="FilterType">The kind of rule the segment uses.</param>
/// <param name="FilterValue">The value the rule needs, or <see langword="null"/> for a rule that takes none.</param>
public sealed record CampaignSegmentDefinition(CampaignFilterType FilterType, string? FilterValue);
