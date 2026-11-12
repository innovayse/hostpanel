using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.StatusPage.Commands.OpenStatusIncident;
using Innovayse.Hostpanel.Application.StatusPage.Commands.PostStatusIncidentUpdate;
using Innovayse.Hostpanel.Application.StatusPage.Common;
using Innovayse.Hostpanel.Domain.StatusPage;
using Innovayse.Hostpanel.Domain.StatusPage.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.StatusPage.Commands.CheckStatusComponentsCron;

/// <summary>
/// Handles <see cref="CheckStatusComponentsCronCommand"/>: requests the address of every monitored component and
/// records the outcome, which moves the component's state (unless an operator overrides it). A component that has
/// failed as many checks in a row as its threshold gets an incident through <see cref="OpenStatusIncidentCommand"/>;
/// when it answers again the incident is closed through <see cref="PostStatusIncidentUpdateCommand"/>. Records
/// older than the retention are deleted at the end of the pass. Re-schedules itself at the configured interval.
/// </summary>
public sealed class CheckStatusComponentsCronHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Requests a component's address.</summary>
    private readonly IStatusProbe _probe;

    /// <summary>Message bus, for the incidents.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Runs the passes under their lease and schedules the next run.</summary>
    private readonly RecurringJobs _jobs;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<CheckStatusComponentsCronHandler> _logger;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="probe">Requests a component's address.</param>
    /// <param name="bus">Message bus, for the incidents.</param>
    /// <param name="jobs">Runs the passes under their lease and schedules the next run.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    public CheckStatusComponentsCronHandler(
        IHostpanelDbContext context,
        IStatusProbe probe,
        IMessageBus bus,
        RecurringJobs jobs,
        ILogger<CheckStatusComponentsCronHandler> logger,
        IClock clock)
    {
        _context = context;
        _probe = probe;
        _bus = bus;
        _jobs = jobs;
        _logger = logger;
        _clock = clock;
    }

    /// <summary>Checks the monitored components, then re-schedules itself.</summary>
    /// <param name="cmd">The scheduled command (marker, no payload).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the message has been handled.</returns>
    public Task HandleAsync(CheckStatusComponentsCronCommand cmd, CancellationToken ct)
    {
        _ = cmd;

        return _jobs.RunAsync<CheckStatusComponentsCronCommand>(
            _jobs.Schedules.CheckStatusComponents, () => RunAsync(ct), ct);
    }

    /// <summary>One pass of the job; <see cref="HandleAsync"/> runs it through <see cref="RecurringJobs"/>.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    private async Task RunAsync(CancellationToken ct)
    {
        var checkedCount = 0;
        var lastId = 0;
        while (true)
        {
            // Tracked: each component is mutated and saved below. A component whose monitoring was switched off
            // while it still holds an incident of ours is read too, so that incident is closed. Keyset batches,
            // so a long list is never loaded at once and a component that fails is not read again this pass.
            var components = await _context.StatusComponents
                .Where(c => c.Id > lastId && (c.MonitoringEnabled || c.ActiveIncidentId != null))
                .OrderBy(c => c.Id)
                .Take(JobBatch.Size)
                .ToListAsync(ct);
            if (components.Count == 0)
            {
                break;
            }

            // The requests are independent and wait on the network, so the batch is probed side by side.
            var outcomes = await Task.WhenAll(components.Select(c => ProbeAsync(c, ct)));
            for (var i = 0; i < components.Count; i++)
            {
                if (await ApplyAsync(components[i], outcomes[i], ct) && outcomes[i] is not null)
                {
                    checkedCount++;
                }
            }

            lastId = components[^1].Id;
            _context.DetachAll();
        }

        var pruned = await PruneAsync(ct);

        _logger.LogInformation(
            "CheckStatusComponentsCron checked {CheckedCount} components and pruned {PrunedCount} old records.", checkedCount, pruned);
    }

    /// <summary>
    /// Deletes the check records older than the retention, in batches. Rows are removed through the change tracker, not
    /// with a bulk delete, which the architecture rules ban for handlers.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How many records were deleted.</returns>
    private async Task<int> PruneAsync(CancellationToken ct)
    {
        var cutoff = _clock.UtcNow - _jobs.Schedules.StatusCheckRetention;
        var pruned = 0;
        while (true)
        {
            var expired = await _context.StatusChecks
                .Where(c => c.CheckedAt < cutoff)
                .OrderBy(c => c.Id)
                .Take(JobBatch.Size)
                .ToListAsync(ct);
            if (expired.Count == 0)
            {
                return pruned;
            }

            _context.StatusChecks.RemoveRange(expired);
            await _context.SaveChangesAsync(ct);
            pruned += expired.Count;
            _context.DetachAll();
        }
    }

    /// <summary>Requests the address of a component whose check is switched on.</summary>
    /// <param name="component">The component.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>What the request found, or <see langword="null"/> when the component is not monitored.</returns>
    private async Task<StatusProbeResult?> ProbeAsync(StatusComponent component, CancellationToken ct)
    {
        if (!component.MonitoringEnabled || component.CheckUrl is null)
        {
            return null;
        }

        return await _probe.ProbeAsync(component.CheckUrl, ct);
    }

    /// <summary>
    /// Records a component's outcome and brings its incident in line with its state. A failure is logged and the
    /// component left as it was stored, so the next component is not affected and the next pass retries.
    /// </summary>
    /// <param name="component">The tracked component.</param>
    /// <param name="outcome">What the request found, or <see langword="null"/> when none was made.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><see langword="true"/> when the component was handled, <see langword="false"/> when it failed.</returns>
    private async Task<bool> ApplyAsync(StatusComponent component, StatusProbeResult? outcome, CancellationToken ct)
    {
        StatusCheck? check = null;
        try
        {
            if (outcome is not null)
            {
                check = component.RecordCheck(_clock.UtcNow, outcome.Succeeded, outcome.LatencyMs);
                _context.StatusChecks.Add(check);
            }

            await _context.SaveChangesAsync(ct);
            await ReconcileIncidentAsync(component, ct);
            return true;
        }
        catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
        {
            // The failed change is still tracked and would ride along with, and fail, every later save in this
            // pass; detaching it isolates this component, and the next run retries it.
            _context.Detach(component);
            if (check is not null)
            {
                _context.Detach(check);
            }

            _logger.LogError(ex, "Unexpected error checking status component {ComponentId}.", component.Id);
            return false;
        }
    }

    /// <summary>
    /// Opens an incident for a component that is down and has none, and closes the incident of one that is not
    /// down. Driven by the stored state rather than by the change, so a pass that failed half way is finished by the next.
    /// </summary>
    /// <param name="component">The tracked component, already saved with its latest check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    private async Task ReconcileIncidentAsync(StatusComponent component, CancellationToken ct)
    {
        if (component.IsDown && component.ActiveIncidentId is null)
        {
            var opened = await _bus.InvokeAsync<Result<int>>(
                new OpenStatusIncidentCommand(
                    $"{component.Name} is not responding",
                    $"Automatic checks of {component.Name} have failed {component.ConsecutiveFailures} times in a row. We are looking into it."),
                ct);
            if (opened.IsFailure)
            {
                _logger.LogWarning("Incident for status component {ComponentId} was refused ({Code}).", component.Id, opened.ErrorCode);
                return;
            }

            component.TrackIncident(opened.Value);
            await _context.SaveChangesAsync(ct);
        }
        else if (!component.IsDown && component.ActiveIncidentId is { } incidentId)
        {
            // An operator may have closed the incident by hand; it then takes no update and only the link is left to drop.
            var stillOpen = await _context.StatusIncidents
                .AnyAsync(i => i.Id == incidentId && i.State != StatusIncidentState.Resolved, ct);
            if (stillOpen)
            {
                var resolved = await _bus.InvokeAsync<Result>(
                    new PostStatusIncidentUpdateCommand(
                        incidentId,
                        StatusIncidentState.Resolved,
                        $"Automatic checks of {component.Name} succeed again. The incident is resolved."),
                    ct);
                if (resolved.IsFailure)
                {
                    _logger.LogWarning("Closing the incident of status component {ComponentId} was refused ({Code}).", component.Id, resolved.ErrorCode);
                    return;
                }
            }

            component.ReleaseIncident();
            await _context.SaveChangesAsync(ct);
        }
    }
}
