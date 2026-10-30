using Innovayse.Hostpanel.Application.Billing.Commands.LapseOverdueInvoice;
using Innovayse.Hostpanel.Application.Billing.Commands.MarkInvoiceOverdue;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.CheckOverdueInvoicesCron;

/// <summary>
/// Handles <see cref="CheckOverdueInvoicesCronCommand"/> by marking all unpaid invoices
/// past their due date as overdue, then lapsing the overdue digital invoices whose digital renewal lines are all
/// past their product grace period. Re-schedules itself for the next day after completion.
/// </summary>
public sealed class CheckOverdueInvoicesCronHandler
{
    /// <summary>The module's data context, for the ids of the unpaid invoices past their due date.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Runs the passes under their lease and schedules the next run.</summary>
    private readonly RecurringJobs _jobs;

    /// <summary>Message bus, for the per-invoice commands.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<CheckOverdueInvoicesCronHandler> _logger;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context, for the ids of the unpaid invoices past their due date.</param>
    /// <param name="jobs">Runs the passes under their lease and schedules the next run.</param>
    /// <param name="bus">Message bus, for the per-invoice commands.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    public CheckOverdueInvoicesCronHandler(
        IHostpanelDbContext context,
        RecurringJobs jobs,
        IMessageBus bus,
        ILogger<CheckOverdueInvoicesCronHandler> logger,
        IClock clock)
    {
        _context = context;
        _jobs = jobs;
        _bus = bus;
        _logger = logger;
        _clock = clock;
    }

    /// <summary>
    /// Finds the unpaid invoices past their due date in batches and sends each a <see cref="MarkInvoiceOverdueCommand"/>.
    /// Each marked invoice raises an <see cref="Innovayse.Hostpanel.Domain.Billing.Events.InvoiceOverdueEvent"/>.
    /// </summary>
    /// <param name="cmd">The scheduled command (marker, no payload).</param>
    /// <param name="ct">Cancellation token.</param>
    public Task HandleAsync(CheckOverdueInvoicesCronCommand cmd, CancellationToken ct)
    {
        _ = cmd;
        var now = _clock.UtcNow;

        return _jobs.RunAsync<CheckOverdueInvoicesCronCommand>(
            _jobs.Schedules.CheckOverdueInvoices, () => RunAsync(now, ct), ct);
    }

    /// <summary>One pass of the job; <see cref="HandleAsync"/> runs it through <see cref="RecurringJobs"/>.</summary>
    /// <param name="now">The moment this pass started.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    private async Task RunAsync(DateTimeOffset now, CancellationToken ct)
    {
        // Overdue first, so an invoice that fell due long ago is marked and then judged for lapse in the same pass.
        var marked = await MarkOverdueAsync(now, ct);
        var lapsed = await LapseAsync(now, ct);

        _logger.LogInformation("CheckOverdueInvoicesCron marked {Count} invoices as overdue and {Lapsed} as lapsed.", marked, lapsed);
    }

    /// <summary>Sends a <see cref="MarkInvoiceOverdueCommand"/> for every unpaid invoice past its due date.</summary>
    /// <param name="now">The moment this pass started.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How many invoices were marked overdue.</returns>
    private async Task<int> MarkOverdueAsync(DateTimeOffset now, CancellationToken ct)
    {
        var marked = 0;
        var lastId = 0;
        while (true)
        {
            // Ids only, in keyset batches: nothing is tracked here, and a row that fails is not read again this pass.
            var invoiceIds = await _context.Invoices.AsNoTracking()
                .Where(i => i.Id > lastId && i.Status == InvoiceStatus.Unpaid && i.DueDate < now)
                .OrderBy(i => i.Id)
                .Select(i => i.Id)
                .Take(JobBatch.Size)
                .ToListAsync(ct);
            if (invoiceIds.Count == 0)
            {
                break;
            }

            foreach (var invoiceId in invoiceIds)
            {
                // One message (own scope and data context) per invoice: one failure cannot poison the rest.
                try
                {
                    var outcome = await _bus.InvokeAsync<Result>(new MarkInvoiceOverdueCommand(invoiceId), ct);
                    if (outcome.IsFailure)
                    {
                        _logger.LogWarning("Marking invoice {InvoiceId} overdue was refused ({ErrorCode}).", invoiceId, outcome.ErrorCode);
                    }
                    else
                    {
                        marked++;
                    }
                }
                catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
                {
                    _logger.LogError(ex, "Marking invoice {InvoiceId} overdue failed; the next run retries it.", invoiceId);
                }
            }

            lastId = invoiceIds[^1];
        }

        return marked;
    }

    /// <summary>
    /// Sends a <see cref="LapseOverdueInvoiceCommand"/> for every overdue, not yet lapsed invoice that has a
    /// digital line. Whether the grace period of its digital renewal lines is over is decided per invoice by
    /// the command, from the product snapshot on each line; this query only narrows the candidates.
    /// </summary>
    /// <param name="now">The moment this pass started.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>How many invoices were marked lapsed.</returns>
    private async Task<int> LapseAsync(DateTimeOffset now, CancellationToken ct)
    {
        var lapsed = 0;
        var lastId = 0;
        while (true)
        {
            // A grace period is never negative, so an invoice not yet past its due date cannot be past grace.
            var invoiceIds = await _context.Invoices.AsNoTracking()
                .Where(i => i.Id > lastId && i.Status == InvoiceStatus.Overdue && i.LapsedAt == null
                    && i.HasDigitalItems && i.DueDate < now)
                .OrderBy(i => i.Id)
                .Select(i => i.Id)
                .Take(JobBatch.Size)
                .ToListAsync(ct);
            if (invoiceIds.Count == 0)
            {
                break;
            }

            foreach (var invoiceId in invoiceIds)
            {
                try
                {
                    var outcome = await _bus.InvokeAsync<Result<bool>>(new LapseOverdueInvoiceCommand(invoiceId), ct);
                    if (outcome.IsFailure)
                    {
                        _logger.LogWarning("Lapsing invoice {InvoiceId} was refused ({ErrorCode}).", invoiceId, outcome.ErrorCode);
                    }
                    else if (outcome.Value)
                    {
                        lapsed++;
                    }
                }
                catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
                {
                    _logger.LogError(ex, "Lapsing invoice {InvoiceId} failed; the next run retries it.", invoiceId);
                }
            }

            lastId = invoiceIds[^1];
        }

        return lapsed;
    }
}
