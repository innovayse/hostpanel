using Innovayse.Hostpanel.Application.Campaigns.Extensions;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Campaigns;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.MultiTenancy;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.CreateEmailCampaign;

/// <summary>Writes a new <see cref="EmailCampaign"/> as a draft, recording the calling admin as its author, and persists it.</summary>
public sealed class CreateEmailCampaignHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>The caller, whose subject becomes the author of the campaign.</summary>
    private readonly ICurrentUserService _caller;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="caller">The caller, whose subject becomes the author of the campaign.</param>
    public CreateEmailCampaignHandler(IHostpanelDbContext context, ICurrentUserService caller)
    {
        _context = context;
        _caller = caller;
    }

    /// <summary>Handles the command.</summary>
    /// <param name="cmd">The campaign to write.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The new campaign id. This use case answers no refusal code itself; a segment value the validator did not catch is refused by the domain as <c>CAMPAIGN_SEGMENT_VALUE_INVALID</c>.</returns>
    public async Task<Result<int>> HandleAsync(CreateEmailCampaignCommand cmd, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(cmd);

        var campaign = EmailCampaign.Create(cmd.Subject, cmd.Body, _caller.RequireUserId(), cmd.Segments.ToDefinitions());

        _context.EmailCampaigns.Add(campaign);
        await _context.SaveChangesAsync(ct);
        return Result<int>.Success(campaign.Id);
    }
}
