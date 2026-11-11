using Innovayse.Hostpanel.Application.Campaigns.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Shared.Models;

namespace Innovayse.Hostpanel.Application.Campaigns.Queries.CountEmailCampaignAudience;

/// <summary>Counts the audience with the same rules sending uses, through <see cref="CampaignAudience"/>.</summary>
public sealed class CountEmailCampaignAudienceHandler
{
    /// <summary>Resolves which clients the segments reach.</summary>
    private readonly CampaignAudience _audience;

    /// <summary>The clock the relative expiry rules are measured from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="audience">Resolves which clients the segments reach.</param>
    /// <param name="clock">The clock the relative expiry rules are measured from.</param>
    public CountEmailCampaignAudienceHandler(CampaignAudience audience, IClock clock)
    {
        _audience = audience;
        _clock = clock;
    }

    /// <summary>Handles the query.</summary>
    /// <param name="query">The segments to count.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of mails sending would make (one per reachable address); never a refusal, because the validator has checked every segment.</returns>
    public async Task<Result<EmailCampaignAudienceDto>> HandleAsync(CountEmailCampaignAudienceQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        var reached = await _audience.ReachAsync(query.Segments, _clock.UtcNow, ct);
        return Result<EmailCampaignAudienceDto>.Success(new EmailCampaignAudienceDto(reached.Count));
    }
}
