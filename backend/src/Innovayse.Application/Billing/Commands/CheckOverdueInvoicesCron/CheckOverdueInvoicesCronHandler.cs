namespace Innovayse.Application.Billing.Commands.CheckOverdueInvoicesCron;

using Innovayse.Application.Common;
using Innovayse.Domain.Billing.Interfaces;
using Microsoft.Extensions.Logging;
using Wolverine;

/// <summary>
/// Handles <see cref="CheckOverdueInvoicesCronCommand"/> by marking all unpaid invoices
/// past their due date as overdue. Re-schedules itself for the next day after completion.
/// </summary>
public sealed class CheckOverdueInvoicesCronHandler(
    IInvoiceRepository invoiceRepo,
    IUnitOfWork uow,
    IMessageBus bus,
    ILogger<CheckOverdueInvoicesCronHandler> logger)
{
    /// <summary>Hour (UTC) at which this job runs daily.</summary>
    private const int CronHour = 7;

    /// <summary>
    /// Queries all unpaid invoices past their due date and transitions them to Overdue status.
    /// Each marked invoice raises an <see cref="Innovayse.Domain.Billing.Events.InvoiceOverdueEvent"/>.
    /// </summary>
    /// <param name="cmd">The scheduled command (marker, no payload).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task HandleAsync(CheckOverdueInvoicesCronCommand cmd, CancellationToken ct)
    {
        _ = cmd;

        var overdueInvoices = await invoiceRepo.ListUnpaidOverdueAsync(DateTimeOffset.UtcNow, ct);

        foreach (var invoice in overdueInvoices)
        {
            invoice.MarkOverdue();
        }

        if (overdueInvoices.Count > 0)
        {
            await uow.SaveChangesAsync(ct);
        }

        logger.LogInformation(
            "CheckOverdueInvoicesCron marked {Count} invoices as overdue.", overdueInvoices.Count);

        // Re-schedule for next day
        var nextRun = NextDailyOccurrence(CronHour);
        await bus.ScheduleAsync(new CheckOverdueInvoicesCronCommand(), nextRun);
        logger.LogInformation("CheckOverdueInvoicesCronCommand re-scheduled for {NextRun:u}.", nextRun);
    }

    /// <summary>
    /// Calculates the next UTC occurrence of the given hour.
    /// If the hour has already passed today, returns the same hour tomorrow.
    /// </summary>
    /// <param name="hour">Target hour in UTC (0-23).</param>
    /// <returns>The next occurrence of the specified hour in UTC.</returns>
    private static DateTimeOffset NextDailyOccurrence(int hour)
    {
        var now = DateTimeOffset.UtcNow;
        var candidate = new DateTimeOffset(now.Year, now.Month, now.Day, hour, 0, 0, TimeSpan.Zero);
        return candidate > now ? candidate : candidate.AddDays(1);
    }
}
