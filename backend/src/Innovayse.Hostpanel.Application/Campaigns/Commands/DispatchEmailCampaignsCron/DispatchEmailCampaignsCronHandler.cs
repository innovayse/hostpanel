using Innovayse.Hostpanel.Application.Campaigns.Commands.DeliverEmailCampaign;
using Innovayse.Hostpanel.Application.Campaigns.Commands.StartEmailCampaign;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Campaigns.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Campaigns.Commands.DispatchEmailCampaignsCron;

/// <summary>
/// Handles <see cref="DispatchEmailCampaignsCronCommand"/>: one pass under the job lease that starts the campaigns that are
/// due and then sends one batch for each campaign that is sending, a campaign started in this pass included. Each campaign is
/// handled by its own message, so one failing campaign cannot hold up the others. Re-schedules itself from the end of the pass.
/// </summary>
public sealed class DispatchEmailCampaignsCronHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Message bus, for the per-campaign messages.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Runs the passes under their lease and schedules the next run.</summary>
    private readonly RecurringJobs _jobs;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<DispatchEmailCampaignsCronHandler> _logger;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Message bus, for the per-campaign messages.</param>
    /// <param name="jobs">Runs the passes under their lease and schedules the next run.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    public DispatchEmailCampaignsCronHandler(
        IHostpanelDbContext context,
        IMessageBus bus,
        RecurringJobs jobs,
        ILogger<DispatchEmailCampaignsCronHandler> logger,
        IClock clock)
    {
        _context = context;
        _bus = bus;
        _jobs = jobs;
        _logger = logger;
        _clock = clock;
    }

    /// <summary>Runs one dispatch pass and re-schedules itself.</summary>
    /// <param name="cmd">Marker.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the pass ended and the next run is scheduled.</returns>
    public Task HandleAsync(DispatchEmailCampaignsCronCommand cmd, CancellationToken ct)
    {
        _ = cmd;

        return _jobs.RunAsync<DispatchEmailCampaignsCronCommand>(_jobs.Schedules.DispatchEmailCampaigns, () => RunAsync(ct), ct);
    }

    /// <summary>One pass of the job; <see cref="HandleAsync"/> runs it through <see cref="RecurringJobs"/>.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when every campaign of the pass was handled.</returns>
    private async Task RunAsync(CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var dueIds = await _context.EmailCampaigns.AsNoTracking().IgnoreAutoIncludes()
            .Where(c => c.Status == CampaignStatus.Scheduled && c.ScheduledAt <= now)
            .OrderBy(c => c.ScheduledAt).ThenBy(c => c.Id)
            .Select(c => c.Id)
            .Take(JobBatch.Size)
            .ToListAsync(ct);
        foreach (var id in dueIds)
        {
            await DispatchAsync(new StartEmailCampaignCommand(id), id, ct);
        }

        var sendingIds = await _context.EmailCampaigns.AsNoTracking().IgnoreAutoIncludes()
            .Where(c => c.Status == CampaignStatus.Sending)
            .OrderBy(c => c.Id)
            .Select(c => c.Id)
            .Take(JobBatch.Size)
            .ToListAsync(ct);
        foreach (var id in sendingIds)
        {
            await DispatchAsync(new DeliverEmailCampaignCommand(id), id, ct);
        }

        _logger.LogInformation("DispatchEmailCampaignsCron started {Started} and delivered for {Sending} campaigns.", dueIds.Count, sendingIds.Count);
    }

    /// <summary>Sends one per-campaign message; a refusal or failure is logged and the next pass tries again.</summary>
    /// <param name="message">The start or deliver message.</param>
    /// <param name="campaignId">The campaign the message is for, for the log.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the message was handled.</returns>
    private async Task DispatchAsync(object message, int campaignId, CancellationToken ct)
    {
        // One message (own scope and DbContext) per campaign: one failure cannot poison the rest.
        try
        {
            var result = await _bus.InvokeAsync<Result>(message, ct);
            if (result.IsFailure)
            {
                _logger.LogWarning("{Message} for campaign {CampaignId} refused ({Code}); the next pass tries again.", message.GetType().Name, campaignId, result.ErrorCode);
            }
        }
        catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
        {
            _logger.LogError(ex, "{Message} for campaign {CampaignId} failed; the next pass tries again.", message.GetType().Name, campaignId);
        }
    }
}
