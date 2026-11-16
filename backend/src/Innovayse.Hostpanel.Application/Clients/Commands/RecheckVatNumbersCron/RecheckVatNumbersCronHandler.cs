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
/// unchecked. A check the service could not complete changes nothing and the client is tried again on the next run. Each country
/// is judged on its own: one whose register keeps answering without a verdict is left out for the rest of the pass, and a country
/// whose register is not set up on this installation is left out altogether, so neither can keep the others from being checked.
/// </summary>
public sealed class RecheckVatNumbersCronHandler
{
    /// <summary>Runs the pass under its lease and schedules the next run.</summary>
    private readonly RecurringJobs _jobs;

    /// <summary>The module's data context, for the clients that are due.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Message bus, to run the check command per client.</summary>
    private readonly IMessageBus _bus;

    /// <summary>The port to the registers, asked which countries can be checked at all.</summary>
    private readonly IVatNumberChecker _checker;

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
    /// <param name="checker">The port to the registers, asked which countries can be checked at all.</param>
    /// <param name="clock">The clock the cut-off date is read from.</param>
    /// <param name="options">The scheduled jobs settings.</param>
    /// <param name="logger">Structured logger.</param>
    public RecheckVatNumbersCronHandler(
        RecurringJobs jobs,
        IHostpanelDbContext context,
        IMessageBus bus,
        IVatNumberChecker checker,
        IClock clock,
        IOptions<ScheduledJobsOptions> options,
        ILogger<RecheckVatNumbersCronHandler> logger)
    {
        _jobs = jobs;
        _context = context;
        _bus = bus;
        _checker = checker;
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

        // A register that is not set up here (the United Kingdom's without credentials) can never answer; its clients are left
        // out, or the same never-judged ones would head the queue every day and take the whole pass for nothing.
        var countries = VatRegisters.Countries.Where(_checker.IsConfiguredFor).ToArray();

        // A country whose register stopped answering is left alone for the rest of the pass, so it never blocks the others.
        var stoppedCountries = new HashSet<string>(StringComparer.Ordinal);
        var unavailableInARow = new Dictionary<string, int>(StringComparer.Ordinal);
        var attemptedIds = new HashSet<int>();
        var checkedCount = 0;
        var unavailable = 0;
        while (attemptedIds.Count < options.VatRecheckBatchSize)
        {
            var openCountries = countries.Where(c => !stoppedCountries.Contains(c)).ToArray();
            var triedIds = attemptedIds.ToArray();

            // Never judged first (false sorts before true), then the oldest check; a closed account is no longer invoiced.
            var due = await _context.Clients
                .AsNoTracking()
                .Where(c => c.TaxId != null && c.Status != ClientStatus.Closed && openCountries.Contains(c.Country!)
                    && !triedIds.Contains(c.Id)
                    && (c.VatStatus == null || c.VatCheckedAt < staleBefore))
                .OrderBy(c => c.VatCheckedAt != null)
                .ThenBy(c => c.VatCheckedAt)
                .ThenBy(c => c.Id)
                .Select(c => new { c.Id, Country = c.Country! })
                .Take(options.VatRecheckBatchSize - attemptedIds.Count)
                .ToListAsync(ct);
            if (due.Count == 0)
            {
                break;
            }

            foreach (var client in due)
            {
                // The country stopped answering earlier in this round: no call, and no place in the batch is used up.
                if (stoppedCountries.Contains(client.Country))
                {
                    continue;
                }

                attemptedIds.Add(client.Id);
                var outcome = await _bus.InvokeAsync<Result>(new CheckClientVatCommand(client.Id), ct);
                if (outcome.IsSuccess)
                {
                    checkedCount++;
                    unavailableInARow.Remove(client.Country);
                    continue;
                }

                if (outcome.ErrorCode != HostpanelErrorCodes.VAT_SERVICE_UNAVAILABLE)
                {
                    // A client that changed since the query (number removed, country moved) is simply not due any more.
                    continue;
                }

                unavailable++;
                var count = unavailableInARow.GetValueOrDefault(client.Country) + 1;
                unavailableInARow[client.Country] = count;
                if (count >= options.VatRecheckStopAfterUnavailable)
                {
                    stoppedCountries.Add(client.Country);
                    _logger.LogWarning(
                        "RecheckVatNumbersCron: the VAT number service gave no verdict {Count} times in a row for {Country}; leaving that country out of the pass.",
                        count, client.Country);
                }
            }
        }

        _logger.LogInformation(
            "RecheckVatNumbersCron: {AttemptedCount} numbers tried, {CheckedCount} checked, {UnavailableCount} without a verdict.",
            attemptedIds.Count, checkedCount, unavailable);
    }
}
