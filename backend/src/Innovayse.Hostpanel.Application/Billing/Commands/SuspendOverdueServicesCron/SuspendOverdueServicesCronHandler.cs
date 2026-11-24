using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Application.Services.Commands.SuspendService;
using Innovayse.Hostpanel.Application.Services.Extensions;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Domain.Services.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.SuspendOverdueServicesCron;

/// <summary>
/// Handles <see cref="SuspendOverdueServicesCronCommand"/>, the daily overdue run: suspends, with
/// <see cref="SuspensionSource.Overdue"/>, only the Active services referenced by lines of invoices that
/// stayed Overdue past <c>billing.renewal.suspend_after_overdue_days</c>. Services billed by hand
/// (<c>AutoRenew</c> off) or under a suspension hold (<c>OverrideAutoSuspend</c>, until its
/// <c>SuspendUntil</c> when it names one) are skipped. Coverage by an
/// overdue invoice is re-checked right before each suspension. Each suspension is its own
/// <see cref="SuspendServiceCommand"/> so one failure cannot stop the rest. Re-schedules itself for the next day.
/// </summary>
public sealed class SuspendOverdueServicesCronHandler
{
    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the grace period.</summary>
    private readonly RenewalSettingsReader _settings;

    /// <summary>Message bus, for the suspensions.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Runs the passes under their lease and schedules the next run.</summary>
    private readonly RecurringJobs _jobs;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<SuspendOverdueServicesCronHandler> _logger;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="settings">Reads the grace period.</param>
    /// <param name="bus">Message bus, for the suspensions.</param>
    /// <param name="jobs">Runs the passes under their lease and schedules the next run.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    public SuspendOverdueServicesCronHandler(
        IHostpanelDbContext context,
        RenewalSettingsReader settings,
        IMessageBus bus,
        RecurringJobs jobs,
        ILogger<SuspendOverdueServicesCronHandler> logger,
        IClock clock)
    {
        _context = context;
        _settings = settings;
        _bus = bus;
        _jobs = jobs;
        _logger = logger;
        _clock = clock;
    }

    /// <summary>Suspends services on invoices overdue past the grace period, then re-schedules itself.</summary>
    /// <param name="cmd">Marker.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    public Task HandleAsync(SuspendOverdueServicesCronCommand cmd, CancellationToken ct)
    {
        _ = cmd;
        var now = _clock.UtcNow;

        return _jobs.RunAsync<SuspendOverdueServicesCronCommand>(
            _jobs.Schedules.SuspendOverdueServices, () => RunAsync(now, ct), ct);
    }

    /// <summary>One pass of the job; <see cref="HandleAsync"/> runs it through <see cref="RecurringJobs"/>.</summary>
    /// <param name="now">The moment this pass started.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    private async Task RunAsync(DateTimeOffset now, CancellationToken ct)
    {
        var settings = await _settings.ReadAsync(ct);
        var cutoff = now.AddDays(-settings.SuspendAfterOverdueDays);

        var serviceIds = await _context.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Overdue && i.DueDate < cutoff)
            .SelectMany(i => i.Items)
            .Where(l => l.ClientServiceId != null)
            .Select(l => l.ClientServiceId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var states = await _bus.GetServiceBillingStatesAsync(serviceIds, ct);
        // A hold (OverrideAutoSuspend) protects a service until its SuspendUntil, or for good when it
        // names no date; a date only ever comes with the hold, so an elapsed one ends it.
        var eligible = states.Values
            .Where(s => s.Status == ServiceStatus.Active && s.AutoRenew && !s.InTrial
                        && (!s.OverrideAutoSuspend || (s.SuspendUntil != null && s.SuspendUntil <= now)))
            .Select(s => s.Id)
            .Order()
            .ToList();

        var suspended = 0;
        foreach (var id in eligible)
        {
            try
            {
                // Re-checked right before each suspension: the list above can be minutes old by
                // now, and a payment that landed meanwhile (its unsuspend already ran and found
                // nothing to lift) must not be answered with a suspension nobody would lift
                // until the next renewal run.
                var stillCovered = await _context.Invoices.AsNoTracking()
                    .AnyAsync(i => i.Status == InvoiceStatus.Overdue && i.DueDate < cutoff
                                   && i.Items.Any(l => l.ClientServiceId == id), ct);
                if (!stillCovered)
                {
                    _logger.LogInformation("Service {ServiceId} is no longer on an invoice overdue past the grace period; not suspended.", id);
                    continue;
                }

                var outcome = await _bus.InvokeAsync<Result>(new SuspendServiceCommand(id, SuspensionSource.Overdue), ct);
                if (outcome.IsFailure)
                {
                    _logger.LogWarning("Overdue suspension refused for service {ServiceId} ({Code}).", id, outcome.ErrorCode);
                }
                else
                {
                    suspended++;
                }
            }
            catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
            {
                _logger.LogError(ex, "Overdue suspension failed for service {ServiceId}; the next run retries it.", id);
            }
        }

        _logger.LogInformation("SuspendOverdueServicesCron suspended {Count} of {Eligible} eligible services.", suspended, eligible.Count);
    }
}
