using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.ProcessTrial;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Services.Commands.ProcessTrialsCron;

/// <summary>
/// Handles <see cref="ProcessTrialsCronCommand"/>, the daily trial run: every running trial (active or awaiting
/// payment) gets one <see cref="ProcessTrialCommand"/>, which sends the reminder due, converts or terminates it.
/// A trial that is refused or fails is logged and left for the next day's run; it never stops the others.
/// Re-schedules itself for the next day.
/// </summary>
public sealed class ProcessTrialsCronHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Message bus, for the per-trial step.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Runs the pass under its lease and schedules the next run.</summary>
    private readonly RecurringJobs _jobs;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<ProcessTrialsCronHandler> _logger;

    /// <summary>The clock the run's start is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="bus">Message bus, for the per-trial step.</param>
    /// <param name="jobs">Runs the pass under its lease and schedules the next run.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    public ProcessTrialsCronHandler(
        IHostpanelDbContext context,
        IMessageBus bus,
        RecurringJobs jobs,
        ILogger<ProcessTrialsCronHandler> logger,
        IClock clock)
    {
        _context = context;
        _bus = bus;
        _jobs = jobs;
        _logger = logger;
        _clock = clock;
    }

    /// <summary>Dispatches today's step for every running trial and re-schedules itself.</summary>
    /// <param name="cmd">Marker.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    public Task HandleAsync(ProcessTrialsCronCommand cmd, CancellationToken ct)
    {
        _ = cmd;
        var now = _clock.UtcNow;

        return _jobs.RunAsync<ProcessTrialsCronCommand>(_jobs.Schedules.ProcessTrials, () => RunAsync(now, ct), ct);
    }

    /// <summary>One pass of the job; <see cref="HandleAsync"/> runs it through <see cref="RecurringJobs"/>.</summary>
    /// <param name="now">The moment this pass started.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    private async Task RunAsync(DateTimeOffset now, CancellationToken ct)
    {
        var count = 0;
        var lastId = 0;
        while (true)
        {
            // Keyed on the trial status alone (IX_ClientServices_TrialStatus_TrialEndsAt): the step itself
            // decides from the trial dates, whatever the service status or renewal date.
            var serviceIds = await _context.ClientServices.AsNoTracking()
                .Where(s => s.Id > lastId
                            && (s.TrialStatus == TrialStatus.Active || s.TrialStatus == TrialStatus.AwaitingPayment))
                .OrderBy(s => s.Id)
                .Select(s => s.Id)
                .Take(JobBatch.Size)
                .ToListAsync(ct);
            if (serviceIds.Count == 0)
            {
                break;
            }

            // One message (own scope and DbContext) per trial: one failure cannot poison the rest.
            foreach (var id in serviceIds)
            {
                try
                {
                    var result = await _bus.InvokeAsync<Result>(new ProcessTrialCommand(id), ct);
                    if (result.IsFailure)
                    {
                        _logger.LogWarning("Trial step for service {ServiceId} refused ({Code}); the next run retries it.", id, result.ErrorCode);
                    }
                }
                catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
                {
                    _logger.LogError(ex, "Trial step for service {ServiceId} failed; the next run retries it.", id);
                }
            }

            count += serviceIds.Count;
            lastId = serviceIds[^1];
        }

        _logger.LogInformation("ProcessTrialsCron processed {Trials} running trials at {Now}.", count, now);
    }
}
