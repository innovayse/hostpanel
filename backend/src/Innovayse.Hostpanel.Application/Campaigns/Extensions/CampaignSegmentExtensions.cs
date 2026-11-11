using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Domain.Campaigns;

namespace Innovayse.Hostpanel.Application.Campaigns.Extensions;

/// <summary>
/// The one place a campaign's segments change shape between the wire (<see cref="CampaignSegmentDto"/>) and the domain
/// (<see cref="CampaignSegmentDefinition"/> going in, <see cref="EmailCampaignSegment"/> coming out).
/// </summary>
public static class CampaignSegmentExtensions
{
    /// <summary>Maps the segments of a campaign to the shape the admin API answers with and the audience is matched by.</summary>
    /// <param name="segments">The segments of a campaign.</param>
    /// <returns>One DTO per segment, in the same order.</returns>
    public static IReadOnlyList<CampaignSegmentDto> ToDtos(this IEnumerable<EmailCampaignSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        return segments.Select(segment => new CampaignSegmentDto(segment.FilterType, segment.FilterValue)).ToList();
    }

    /// <summary>Maps the segments a caller wrote to what the campaign takes.</summary>
    /// <param name="segments">The segments as the admin API received them, already checked by the validator.</param>
    /// <returns>One definition per segment, in the same order.</returns>
    public static IReadOnlyList<CampaignSegmentDefinition> ToDefinitions(this IEnumerable<CampaignSegmentDto> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        return segments.Select(segment => new CampaignSegmentDefinition(segment.FilterType, segment.FilterValue)).ToList();
    }
}
