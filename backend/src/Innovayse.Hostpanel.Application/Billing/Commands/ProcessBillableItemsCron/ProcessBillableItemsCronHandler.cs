using Innovayse.Hostpanel.Application.Billing.Commands.InvoiceClientBillableItems;
using Innovayse.Hostpanel.Application.Billing.Extensions;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.ProcessBillableItemsCron;

/// <summary>
/// Handles <see cref="ProcessBillableItemsCronCommand"/> by invoicing all billable items
/// that are due for automatic processing, including recurring items.
/// Re-schedules itself for the next day after completion.
/// </summary>
public sealed class ProcessBillableItemsCronHandler
{
    /// <summary>The module's data context, for the clients that have billable items due.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Message bus, for the per-client invoicing commands.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Runs the passes under their lease and schedules the next run.</summary>
    private readonly RecurringJobs _jobs;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<ProcessBillableItemsCronHandler> _logger;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context, for the clients that have billable items due.</param>
    /// <param name="bus">Message bus, for the per-client invoicing commands.</param>
    /// <param name="jobs">Runs the passes under their lease and schedules the next run.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    public ProcessBillableItemsCronHandler(
        IHostpanelDbContext context,
        IMessageBus bus,
        RecurringJobs jobs,
        ILogger<ProcessBillableItemsCronHandler> logger,
        IClock clock)
    {
        _context = context;
        _bus = bus;
        _jobs = jobs;
        _logger = logger;
        _clock = clock;
    }

    /// <summary>
    /// Processes all due billable items and creates invoices grouped by client.
    /// </summary>
    /// <param name="cmd">The scheduled command (marker, no payload).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the message has been handled.</returns>
    public Task HandleAsync(ProcessBillableItemsCronCommand cmd, CancellationToken ct)
    {
        _ = cmd;
        var now = _clock.UtcNow;

        return _jobs.RunAsync<ProcessBillableItemsCronCommand>(
            _jobs.Schedules.ProcessBillableItems, () => RunAsync(now, ct), ct);
    }

    /// <summary>One pass of the job; <see cref="HandleAsync"/> runs it through <see cref="RecurringJobs"/>.</summary>
    /// <param name="now">The moment this pass started.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    private async Task RunAsync(DateTimeOffset now, CancellationToken ct)
    {
        await InvoiceDueClientsAsync(BillableItemType.OneTime, now, ct);
        await InvoiceDueClientsAsync(BillableItemType.Recurring, now, ct);
    }

    /// <summary>
    /// Walks the clients with due items of one type in batches of ids (keyset on the client id, so a
    /// client that fails is not read again this pass) and dispatches one command per client. Each
    /// command runs in its own message scope and data context: one client's failure is logged and
    /// leaves nothing tracked that could fail the next client.
    /// </summary>
    /// <param name="type">Whether the one-time or the recurring items are invoiced.</param>
    /// <param name="now">The moment this pass started.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    private async Task InvoiceDueClientsAsync(BillableItemType type, DateTimeOffset now, CancellationToken ct)
    {
        var lastClientId = 0;
        while (true)
        {
            var clientIds = await _context.BillableItems.AsNoTracking()
                .DueForInvoicing(type, now)
                .Where(x => x.ClientId > lastClientId)
                .Select(x => x.ClientId)
                .Distinct()
                .OrderBy(id => id)
                .Take(JobBatch.Size)
                .ToListAsync(ct);
            if (clientIds.Count == 0)
            {
                return;
            }

            foreach (var clientId in clientIds)
            {
                try
                {
                    var outcome = await _bus.InvokeAsync<Result>(new InvoiceClientBillableItemsCommand(clientId, type), ct);
                    if (outcome.IsFailure)
                    {
                        _logger.LogWarning(
                            "Invoicing {Type} billable items of client {ClientId} was refused ({ErrorCode}); the next run retries it.",
                            type, clientId, outcome.ErrorCode);
                    }
                }
                catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
                {
                    _logger.LogError(
                        ex, "Invoicing {Type} billable items of client {ClientId} failed; the next run retries it.", type, clientId);
                }
            }

            lastClientId = clientIds[^1];
        }
    }
}
