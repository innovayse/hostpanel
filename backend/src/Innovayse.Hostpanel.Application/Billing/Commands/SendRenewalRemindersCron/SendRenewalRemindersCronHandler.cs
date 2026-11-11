using Innovayse.Hostpanel.Application.Billing.Commands.SendRenewalReminder;
using Innovayse.Hostpanel.Application.Billing.Common;
using Innovayse.Hostpanel.Application.Common;
using Innovayse.Hostpanel.Application.Common.Interfaces;
using Innovayse.Hostpanel.Domain.Billing.Enums;
using Innovayse.Hostpanel.Shared.Infrastructure;
using Innovayse.Hostpanel.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace Innovayse.Hostpanel.Application.Billing.Commands.SendRenewalRemindersCron;

/// <summary>
/// Handles <see cref="SendRenewalRemindersCronCommand"/>, the daily reminder run: every unpaid
/// renewal invoice that has reached one of the configured thresholds (<c>billing.renewal.reminder_days</c>)
/// gets one reminder mail per threshold, recorded in <c>InvoiceReminders</c> so it is never repeated.
/// Each invoice is handled by its own <see cref="SendRenewalReminderCommand"/>. Re-schedules itself for the next day.
/// </summary>
public sealed class SendRenewalRemindersCronHandler
{
    /// <summary>
    /// How many days past its due date an unpaid invoice can still be reminded: the per-invoice handler
    /// computes the days left rounded up and sends nothing once that is negative, so an invoice more
    /// than a day late has no reminder left to send.
    /// </summary>
    private const int LateReminderDays = 1;

    /// <summary>The module's data context.</summary>
    private readonly IHostpanelDbContext _context;

    /// <summary>Reads the reminder thresholds.</summary>
    private readonly RenewalSettingsReader _settings;

    /// <summary>Message bus, for the mails.</summary>
    private readonly IMessageBus _bus;

    /// <summary>Runs the passes under their lease and schedules the next run.</summary>
    private readonly RecurringJobs _jobs;

    /// <summary>Structured logger.</summary>
    private readonly ILogger<SendRenewalRemindersCronHandler> _logger;

    /// <summary>The clock every moment in this handler is read from.</summary>
    private readonly IClock _clock;

    /// <summary>Initializes the handler.</summary>
    /// <param name="context">The module's data context.</param>
    /// <param name="settings">Reads the reminder thresholds.</param>
    /// <param name="bus">Message bus, for the mails.</param>
    /// <param name="jobs">Runs the passes under their lease and schedules the next run.</param>
    /// <param name="logger">Structured logger.</param>
    /// <param name="clock">The clock the current moment is read from.</param>
    public SendRenewalRemindersCronHandler(
        IHostpanelDbContext context,
        RenewalSettingsReader settings,
        IMessageBus bus,
        RecurringJobs jobs,
        ILogger<SendRenewalRemindersCronHandler> logger,
        IClock clock)
    {
        _context = context;
        _settings = settings;
        _bus = bus;
        _jobs = jobs;
        _logger = logger;
        _clock = clock;
    }

    /// <summary>Dispatches today's reminders, one message per invoice, and re-schedules itself.</summary>
    /// <param name="cmd">Marker.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    public Task HandleAsync(SendRenewalRemindersCronCommand cmd, CancellationToken ct)
    {
        _ = cmd;
        var now = _clock.UtcNow;

        return _jobs.RunAsync<SendRenewalRemindersCronCommand>(
            _jobs.Schedules.SendRenewalReminders, () => RunAsync(now, ct), ct);
    }

    /// <summary>One pass of the job; <see cref="HandleAsync"/> runs it through <see cref="RecurringJobs"/>.</summary>
    /// <param name="now">The moment this pass started.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task.</returns>
    private async Task RunAsync(DateTimeOffset now, CancellationToken ct)
    {
        var settings = await _settings.ReadAsync(ct);
        if (settings.ReminderDays.Count == 0)
        {
            return;
        }

        var horizon = now.AddDays(settings.ReminderDays.Max() + 1);

        // SendRenewalReminderHandler sends nothing once an invoice is more than a day past due, so
        // such invoices are not read at all: without this bound every long-overdue unpaid invoice
        // would be walked, and dispatched, on every daily run for good.
        var lastDue = now.AddDays(-LateReminderDays);
        var checkedCount = 0;
        var lastId = 0;
        while (true)
        {
            var invoiceIds = await _context.Invoices.AsNoTracking()
                .Where(i => i.Id > lastId
                            && i.Status == InvoiceStatus.Unpaid
                            && i.DueDate <= horizon
                            && i.DueDate > lastDue
                            && i.Items.Any(l => l.RenewalPeriodStart != null))
                .OrderBy(i => i.Id)
                .Select(i => i.Id)
                .Take(JobBatch.Size)
                .ToListAsync(ct);
            if (invoiceIds.Count == 0)
            {
                break;
            }

            // One message (own scope and DbContext) per invoice: one failure cannot poison the rest.
            foreach (var id in invoiceIds)
            {
                try
                {
                    var result = await _bus.InvokeAsync<Result>(new SendRenewalReminderCommand(id), ct);
                    if (result.IsFailure)
                    {
                        _logger.LogWarning("Renewal reminder for invoice {InvoiceId} refused ({Code}); the next run retries it.", id, result.ErrorCode);
                    }
                }
                catch (Exception ex) when (JobFailure.IsFailure(ex, ct))
                {
                    _logger.LogError(ex, "Renewal reminder for invoice {InvoiceId} failed; the next run retries it.", id);
                }
            }

            checkedCount += invoiceIds.Count;
            lastId = invoiceIds[^1];
        }

        _logger.LogInformation("SendRenewalRemindersCron checked {Invoices} open renewal invoices.", checkedCount);
    }
}
