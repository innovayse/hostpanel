namespace Innovayse.API.Billing;

using Innovayse.Application.Billing.Commands.CheckOverdueInvoicesCron;
using Innovayse.Application.Billing.Commands.ProcessBillableItemsCron;
using Innovayse.Application.Billing.Commands.ProcessRenewalsCron;
using Wolverine;

/// <summary>
/// Hosted service that schedules the daily billable items processing job on application startup.
/// Runs at 06:00 UTC daily, processing cron-flagged and recurring billable items.
/// </summary>
public sealed class BillingScheduledJobsStartup(
    IServiceScopeFactory scopeFactory,
    ILogger<BillingScheduledJobsStartup> logger) : IHostedService
{
    /// <summary>Hour (UTC) at which <see cref="ProcessBillableItemsCronCommand"/> should execute.</summary>
    private const int BillableItemsCronHour = 6;

    /// <summary>Hour (UTC) at which <see cref="CheckOverdueInvoicesCronCommand"/> should execute.</summary>
    private const int OverdueCronHour = 7;

    /// <summary>Hour (UTC) at which <see cref="ProcessRenewalsCronCommand"/> should execute.</summary>
    private const int RenewalsCronHour = 5;

    /// <summary>
    /// Schedules the billable items cron job for its next daily occurrence.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token provided by the host.</param>
    /// <returns>A <see cref="Task"/> that completes once the message is enqueued.</returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var bus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        var billableItemsRun = NextDailyOccurrence(BillableItemsCronHour);
        await bus.ScheduleAsync(new ProcessBillableItemsCronCommand(), billableItemsRun);
        logger.LogInformation(
            "ProcessBillableItemsCronCommand scheduled for {ScheduledAt:u}.",
            billableItemsRun);

        var overdueRun = NextDailyOccurrence(OverdueCronHour);
        await bus.ScheduleAsync(new CheckOverdueInvoicesCronCommand(), overdueRun);
        logger.LogInformation(
            "CheckOverdueInvoicesCronCommand scheduled for {ScheduledAt:u}.",
            overdueRun);

        var renewalsRun = NextDailyOccurrence(RenewalsCronHour);
        await bus.ScheduleAsync(new ProcessRenewalsCronCommand(), renewalsRun);
        logger.LogInformation(
            "ProcessRenewalsCronCommand scheduled for {ScheduledAt:u}.",
            renewalsRun);
    }

    /// <summary>
    /// No-op — scheduled messages persist in the outbox; no cleanup is required on shutdown.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token provided by the host.</param>
    /// <returns>A completed <see cref="Task"/>.</returns>
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <summary>
    /// Calculates the next UTC <see cref="DateTimeOffset"/> at which the given hour occurs.
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
