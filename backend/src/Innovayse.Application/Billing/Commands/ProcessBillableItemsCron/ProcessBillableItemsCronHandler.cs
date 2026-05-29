namespace Innovayse.Application.Billing.Commands.ProcessBillableItemsCron;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing;
using Innovayse.Domain.Billing.Interfaces;
using Microsoft.Extensions.Logging;
using Wolverine;

/// <summary>
/// Handles <see cref="ProcessBillableItemsCronCommand"/> by invoicing all billable items
/// that are due for automatic processing, including recurring items.
/// Re-schedules itself for the next day after completion.
/// </summary>
public sealed class ProcessBillableItemsCronHandler(
    IBillableItemRepository billableItemRepo,
    IInvoiceRepository invoiceRepo,
    IUnitOfWork uow,
    IMessageBus bus,
    ILogger<ProcessBillableItemsCronHandler> logger)
{
    /// <summary>Hour (UTC) at which this job runs daily.</summary>
    private const int CronHour = 6;

    /// <summary>Default payment term in days for auto-generated invoices.</summary>
    private const int PaymentTermDays = 14;

    /// <summary>
    /// Processes all due billable items and creates invoices grouped by client.
    /// </summary>
    /// <param name="cmd">The scheduled command (marker, no payload).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(ProcessBillableItemsCronCommand cmd, CancellationToken ct)
    {
        _ = cmd;

        await ProcessCronItemsAsync(ct);
        await ProcessRecurringItemsAsync(ct);

        // Re-schedule for next day
        var nextRun = NextDailyOccurrence(CronHour);
        await bus.ScheduleAsync(new ProcessBillableItemsCronCommand(), nextRun);
        logger.LogInformation("ProcessBillableItemsCronCommand re-scheduled for {NextRun:u}.", nextRun);
    }

    /// <summary>
    /// Invoices all one-time cron items (InvoiceOnNextCron, InvoiceForDueDate with DueDate &lt;= now).
    /// Groups items by client and creates one invoice per client.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    private async Task ProcessCronItemsAsync(CancellationToken ct)
    {
        var items = await billableItemRepo.GetDueForCronInvoicingAsync(ct);
        if (items.Count == 0)
        {
            return;
        }

        var grouped = items.GroupBy(i => i.ClientId);

        foreach (var group in grouped)
        {
            var clientId = group.Key;
            var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(PaymentTermDays));

            foreach (var item in group)
            {
                invoice.AddItem(item.Description, item.Amount, 1);
            }

            invoiceRepo.Add(invoice);
            await uow.SaveChangesAsync(ct);

            foreach (var item in group)
            {
                item.MarkInvoiced(invoice.Id);
            }

            await uow.SaveChangesAsync(ct);

            logger.LogInformation(
                "Created invoice {InvoiceId} for client {ClientId} with {Count} cron billable items.",
                invoice.Id, clientId, group.Count());
        }
    }

    /// <summary>
    /// Processes recurring billable items whose due date has arrived.
    /// Creates an invoice for each recurring item, increments the invoice count,
    /// advances the due date, and resets for the next cycle.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    private async Task ProcessRecurringItemsAsync(CancellationToken ct)
    {
        var items = await billableItemRepo.GetDueForRecurrenceAsync(ct);
        if (items.Count == 0)
        {
            return;
        }

        var grouped = items.GroupBy(i => i.ClientId);

        foreach (var group in grouped)
        {
            var clientId = group.Key;
            var invoice = Invoice.Create(clientId, DateTimeOffset.UtcNow.AddDays(PaymentTermDays));

            foreach (var item in group)
            {
                invoice.AddItem(item.Description, item.Amount, 1);
            }

            invoiceRepo.Add(invoice);
            await uow.SaveChangesAsync(ct);

            foreach (var item in group)
            {
                item.MarkInvoiced(invoice.Id);
                item.AdvanceDueDate();
            }

            await uow.SaveChangesAsync(ct);

            logger.LogInformation(
                "Created recurring invoice {InvoiceId} for client {ClientId} with {Count} items.",
                invoice.Id, clientId, group.Count());
        }
    }

    /// <summary>
    /// Calculates the next UTC occurrence of the given hour.
    /// If the hour has already passed today, returns the same hour tomorrow.
    /// </summary>
    /// <param name="hour">Target hour in UTC (0–23).</param>
    /// <returns>The next occurrence of the specified hour in UTC.</returns>
    private static DateTimeOffset NextDailyOccurrence(int hour)
    {
        var now = DateTimeOffset.UtcNow;
        var candidate = new DateTimeOffset(now.Year, now.Month, now.Day, hour, 0, 0, TimeSpan.Zero);
        return candidate > now ? candidate : candidate.AddDays(1);
    }
}
