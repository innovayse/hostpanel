using Innovayse.Hostpanel.Application.Clients.Commands.CheckClientVat;
using Innovayse.Hostpanel.Application.Clients.Common;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Errors;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Clients.Enums;
using Innovayse.Hostpanel.Shared.Models;
using Innovayse.Hostpanel.Shared.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Clients.Commands.RecheckVatNumbersCron;

/// <summary>
/// Handles <see cref="RecheckVatNumbersCronCommand"/> by running the same check as the client page's button
/// (<see cref="CheckClientVatCommand"/>) for the clients whose number is due: never judged first, then the longest
/// unchecked. A check the service could not complete changes nothing and the client is tried again on the next run.
/// </summary>
public sealed class RecheckVatNumbersCronHandler
{
    /// <summary>Runs the pass under its lease and schedules the next run.</summary>
    private readonly RecurringJobs _jobs;

    /// <summary>The module's data context, for the clients that are due.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Message bus, to run the check command per client.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The clock the cut-off date is read from.</summary>
    private readonly IClock _clock;

    /// <summary>The scheduled jobs settings: the switch, the age that makes a check due, the pass size and when to give up.</summary>
    private readonly IOptions<ScheduledJobsOptions> _options;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<RecheckVatNumbersCronHandler> _logger;

    /// <summary>Initializes the handler.</summary>
    /// <param name="jobs">Runs the pass under its lease and schedules the next run.</param>
    /// <param name="context">The module's data context, for the clients that are due.</param>
    /// <param name="bus">Message bus, to run the check command per client.</param>
    /// <param name="clock">The clock the cut-off date is read from.</param>
    /// <param name="options">The scheduled jobs settings.</param>
    /// <param name="logger">Structured logger.</param>
    public RecheckVatNumbersCronHandler(
        RecurringJobs jobs,
        IHostpanelDbContext context,
        IMessageBus bus,
        IClock clock,
        IOptions<ScheduledJobsOptions> options,
        ILogger<RecheckVatNumbersCronHandler> logger)
    {
        _jobs = jobs;
        _context = context;
        _bus = bus;
        _clock = clock;
        _options = options;
        _logger = logger;
    }

    /// <summary>Re-checks the numbers that are due and re-schedules itself for the next day.</summary>
    /// <param name="cmd">The scheduled command (marker, no payload).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the message has been handled.</returns>
    public Task HandleAsync(RecheckVatNumbersCronCommand cmd, CancellationToken ct)
    {
        _ = cmd;
        return _jobs.RunAsync<RecheckVatNumbersCronCommand>(_jobs.Schedules.RecheckVatNumbers, () => RunAsync(ct), ct);
    }

    /// <summary>One pass of the job; <see cref="HandleAsync"/> runs it through <see cref="RecurringJobs"/>.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the pass is over.</returns>
    private async Task RunAsync(CancellationToken ct)
    {
        var options = _options.Value;
        if (options.DisableVatRechecks)
        {
            _logger.LogInformation("RecheckVatNumbersCron: VAT number re-checks are disabled; skipping.");
            return;
        }

        var staleBefore = _clock.UtcNow - options.VatRecheckAfter;
        var countries = VatRegisters.Countries.ToArray();

        // Never judged first (false sorts before true), then the oldest check; a closed account is no longer invoiced.
        var due = await _context.Clients
            .AsNoTracking()
            .Where(c => c.TaxId != null && c.Status != ClientStatus.Closed && countries.Contains(c.Country!)
                && (c.VatStatus == null || c.VatCheckedAt < staleBefore))
            .OrderBy(c => c.VatCheckedAt != null)
            .ThenBy(c => c.VatCheckedAt)
            .ThenBy(c => c.Id)
            .Select(c => c.Id)
            .Take(options.VatRecheckBatchSize)
            .ToListAsync(ct);

        var checkedCount = 0;
        var unavailable = 0;
        var consecutiveUnavailable = 0;
        foreach (var clientId in due)
        {
            var outcome = await _bus.InvokeAsync<Result>(new CheckClientVatCommand(clientId), ct);
            if (outcome.IsSuccess)
            {
                checkedCount++;
                consecutiveUnavailable = 0;
                continue;
            }

            if (outcome.ErrorCode != HostpanelErrorCodes.VAT_SERVICE_UNAVAILABLE)
            {
                // A client that changed since the query (number removed, country moved) is simply not due any more.
                consecutiveUnavailable = 0;
                continue;
            }

            unavailable++;
            consecutiveUnavailable++;
            if (consecutiveUnavailable >= options.VatRecheckStopAfterUnavailable)
            {
                _logger.LogWarning(
                    "RecheckVatNumbersCron: the VAT number service gave no verdict {Count} times in a row; stopping the pass.",
                    consecutiveUnavailable);
                break;
            }
        }

        _logger.LogInformation(
            "RecheckVatNumbersCron: {DueCount} numbers due, {CheckedCount} checked, {UnavailableCount} without a verdict.",
            due.Count, checkedCount, unavailable);
    }
}
