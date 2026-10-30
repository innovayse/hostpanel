using System.ComponentModel.DataAnnotations;
using Innovayse.Hostpanel.Shared.Infrastructure;

namespace Innovayse.Hostpanel.Shared.Settings;

/// <summary>
/// When every recurring job runs, and how the instances of the application share them. The startup
/// services and the job handlers read the same values here, so an hour or an interval is stated
/// once. Bound from the <c>ScheduledJobs</c> section; every value has a safe default.
/// </summary>
public sealed class ScheduledJobsOptions : IValidatableObject
{
    /// <summary>The configuration section these options bind from.</summary>
    public const string SectionName = "ScheduledJobs";

    /// <summary>UTC hour (0-23) of the daily sync of domain TLD cost prices.</summary>
    [Range(0, 23)]
    public int SyncTldCostPricesHour { get; init; } = 4;

    /// <summary>UTC hour (0-23) of the daily renewal run.</summary>
    [Range(0, 23)]
    public int ProcessRenewalsHour { get; init; } = 5;

    /// <summary>UTC hour (0-23) of the daily billable items run.</summary>
    [Range(0, 23)]
    public int ProcessBillableItemsHour { get; init; } = 6;

    /// <summary>UTC hour (0-23) of the daily renewal reminders.</summary>
    [Range(0, 23)]
    public int SendRenewalRemindersHour { get; init; } = 6;

    /// <summary>UTC hour (0-23) of the daily server account count sync.</summary>
    [Range(0, 23)]
    public int SyncServerAccountCountsHour { get; init; } = 6;

    /// <summary>UTC hour (0-23) of the daily overdue invoice check.</summary>
    [Range(0, 23)]
    public int CheckOverdueInvoicesHour { get; init; } = 7;

    /// <summary>UTC hour (0-23) of the daily auto-termination of long-suspended services.</summary>
    [Range(0, 23)]
    public int AutoTerminateSuspendedHour { get; init; } = 8;

    /// <summary>UTC hour (0-23) of the daily suspension of services with overdue invoices.</summary>
    [Range(0, 23)]
    public int SuspendOverdueServicesHour { get; init; } = 9;

    /// <summary>UTC hour (0-23) of the daily domain expiry check.</summary>
    [Range(0, 23)]
    public int CheckDomainExpiriesHour { get; init; } = 9;

    /// <summary>UTC hour (0-23) of the daily GitHub release update check.</summary>
    [Range(0, 23)]
    public int CheckReleaseUpdatesHour { get; init; } = 10;

    /// <summary>
    /// When <see langword="true"/>, the daily release-update check is skipped entirely and no
    /// outbound request is made to GitHub. Set this for installations that have no outbound
    /// internet access.
    /// </summary>
    public bool DisableReleaseUpdateChecks { get; init; }

    /// <summary>Minutes between the end of one gateway payment reconciliation and the start of the next.</summary>
    [Range(1, 1440)]
    public int ReconcileGatewayPaymentsIntervalMinutes { get; init; } = 5;

    /// <summary>
    /// Minutes a gateway payment session must have been running before the reconciler first asks the
    /// gateway about it. Hosted sessions last 20 minutes, so anything younger may still be on the
    /// payment page; the default leaves a margin past that.
    /// </summary>
    [Range(1, 1440)]
    public int GatewaySessionSettleMinutes { get; init; } = 25;

    /// <summary>
    /// Minutes the reconciler waits after its first ask about a still pending session before it asks
    /// again; every later wait doubles, up to <see cref="GatewayCheckMaxIntervalMinutes"/>.
    /// </summary>
    [Range(1, 1440)]
    public int GatewayCheckBackoffStartMinutes { get; init; } = 5;

    /// <summary>
    /// Upper bound, in minutes, of the wait between two asks about the same pending session, so a
    /// payment that stays pending is still looked at a few times a day.
    /// </summary>
    [Range(1, 1440)]
    public int GatewayCheckMaxIntervalMinutes { get; init; } = 360;

    /// <summary>
    /// Hours after which a gateway session still pending counts as stalled: staff are told once, and
    /// a settled invoice's leftover session is no longer swept for late money.
    /// </summary>
    [Range(1, 720)]
    public int GatewayStalledAfterHours { get; init; } = 24;

    /// <summary>Minutes between the end of one pending domain status sync and the start of the next.</summary>
    [Range(1, 1440)]
    public int SyncPendingDomainStatusesIntervalMinutes { get; init; } = 15;

    /// <summary>Seconds between the end of one fulfilment dispatch pass and the start of the next.</summary>
    [Range(1, 3600)]
    public int DispatchFulfilmentIntervalSeconds { get; init; } = 30;

    /// <summary>
    /// How many fulfilment deliveries one dispatch pass makes at most. A pass stops there and the next
    /// one carries on, so a backlog never keeps one pass (and the job it holds) running without end.
    /// </summary>
    [Range(1, 500)]
    public int FulfilmentBatchSize { get; init; } = 50;

    /// <summary>
    /// Seconds a fulfilment plugin has to answer one delivery before the attempt counts as failed, so a
    /// plugin that never answers cannot hold the dispatch pass. The claim on the record lasts twice as long.
    /// </summary>
    [Range(1, 600)]
    public int FulfilmentCallTimeoutSeconds { get; init; } = 60;

    /// <summary>
    /// Minutes a pass may hold its job before another instance may take it over. Longer than the
    /// longest pass, or a slow pass is started a second time while it is still running.
    /// </summary>
    [Range(1, 1440)]
    public int LeaseMinutes { get; init; } = 60;

    /// <summary>
    /// Seconds a job stays closed to other instances after a pass succeeded, so an instance whose
    /// timer fires just after the pass finished does not repeat it. 0 turns the rest off.
    /// </summary>
    [Range(0, 3600)]
    public int RestSeconds { get; init; } = 60;

    /// <summary>Minutes between the end of one recovery of stalled migration pulls and the start of the next.</summary>
    [Range(1, 1440)]
    public int RecoverInterruptedMigrationJobsIntervalMinutes { get; init; } = 5;

    /// <summary>
    /// Seconds between the heartbeats a running migration pull writes. A pull saves its progress after
    /// every record, so this only bounds how often that save also refreshes the heartbeat.
    /// </summary>
    [Range(1, 600)]
    public int MigrationHeartbeatSeconds { get; init; } = 30;

    /// <summary>
    /// Minutes without a heartbeat after which a queued or running migration job counts as abandoned
    /// and is failed so it can be retried. Longer than the slowest page the source may take to answer.
    /// </summary>
    [Range(1, 1440)]
    public int MigrationStaleAfterMinutes { get; init; } = 15;

    /// <summary>How many times scheduling a job's next run is attempted before it is given up and logged.</summary>
    [Range(1, 10)]
    public int ScheduleAttempts { get; init; } = 5;

    /// <summary>Seconds waited between two attempts to schedule a job's next run.</summary>
    [Range(0, 600)]
    public int ScheduleRetryDelaySeconds { get; init; } = 5;

    /// <summary>Gets the schedule of the TLD cost price sync.</summary>
    public JobSchedule SyncTldCostPrices => JobSchedule.Daily(SyncTldCostPricesHour);

    /// <summary>Gets the schedule of the renewal run.</summary>
    public JobSchedule ProcessRenewals => JobSchedule.Daily(ProcessRenewalsHour);

    /// <summary>Gets the schedule of the billable items run.</summary>
    public JobSchedule ProcessBillableItems => JobSchedule.Daily(ProcessBillableItemsHour);

    /// <summary>Gets the schedule of the renewal reminders.</summary>
    public JobSchedule SendRenewalReminders => JobSchedule.Daily(SendRenewalRemindersHour);

    /// <summary>Gets the schedule of the server account count sync.</summary>
    public JobSchedule SyncServerAccountCounts => JobSchedule.Daily(SyncServerAccountCountsHour);

    /// <summary>Gets the schedule of the overdue invoice check.</summary>
    public JobSchedule CheckOverdueInvoices => JobSchedule.Daily(CheckOverdueInvoicesHour);

    /// <summary>Gets the schedule of the auto-termination of long-suspended services.</summary>
    public JobSchedule AutoTerminateSuspended => JobSchedule.Daily(AutoTerminateSuspendedHour);

    /// <summary>Gets the schedule of the suspension of services with overdue invoices.</summary>
    public JobSchedule SuspendOverdueServices => JobSchedule.Daily(SuspendOverdueServicesHour);

    /// <summary>Gets the schedule of the domain expiry check.</summary>
    public JobSchedule CheckDomainExpiries => JobSchedule.Daily(CheckDomainExpiriesHour);

    /// <summary>Gets the schedule of the GitHub release update check.</summary>
    public JobSchedule CheckReleaseUpdates => JobSchedule.Daily(CheckReleaseUpdatesHour);

    /// <summary>Gets the schedule of the gateway payment reconciliation.</summary>
    public JobSchedule ReconcileGatewayPayments =>
        JobSchedule.Every(TimeSpan.FromMinutes(ReconcileGatewayPaymentsIntervalMinutes));

    /// <summary>Gets the schedule of the pending domain status sync.</summary>
    public JobSchedule SyncPendingDomainStatuses =>
        JobSchedule.Every(TimeSpan.FromMinutes(SyncPendingDomainStatusesIntervalMinutes));

    /// <summary>Gets the schedule of the fulfilment dispatch.</summary>
    public JobSchedule DispatchFulfilment =>
        JobSchedule.Every(TimeSpan.FromSeconds(DispatchFulfilmentIntervalSeconds));

    /// <summary>Gets how long a fulfilment plugin has to answer one delivery, as a span.</summary>
    public TimeSpan FulfilmentCallTimeout => TimeSpan.FromSeconds(FulfilmentCallTimeoutSeconds);

    /// <summary>
    /// Gets how long a dispatcher's claim on a fulfilment record lasts: twice the time the plugin has to
    /// answer, so the claim outlives the call and the save of its outcome. A dispatcher that dies in
    /// between is noticed once the claim has run out.
    /// </summary>
    public TimeSpan FulfilmentLease => FulfilmentCallTimeout * 2;

    /// <summary>Gets how long a pass holds its job, as a span.</summary>
    public TimeSpan Lease => TimeSpan.FromMinutes(LeaseMinutes);

    /// <summary>Gets how long a successful pass keeps its job closed to other instances, as a span.</summary>
    public TimeSpan Rest => TimeSpan.FromSeconds(RestSeconds);

    /// <summary>Gets the wait between two attempts to schedule a next run, as a span.</summary>
    public TimeSpan ScheduleRetryDelay => TimeSpan.FromSeconds(ScheduleRetryDelaySeconds);

    /// <summary>Gets the schedule of the recovery of stalled migration pulls.</summary>
    public JobSchedule RecoverInterruptedMigrationJobs =>
        JobSchedule.Every(TimeSpan.FromMinutes(RecoverInterruptedMigrationJobsIntervalMinutes));

    /// <summary>Gets the time between the heartbeats of a running migration pull, as a span.</summary>
    public TimeSpan MigrationHeartbeat => TimeSpan.FromSeconds(MigrationHeartbeatSeconds);

    /// <summary>Gets how long a migration job may go without a heartbeat before it counts as abandoned, as a span.</summary>
    public TimeSpan MigrationStaleAfter => TimeSpan.FromMinutes(MigrationStaleAfterMinutes);

    /// <summary>Gets how old a gateway session must be before the reconciler first asks about it, as a span.</summary>
    public TimeSpan GatewaySessionSettle => TimeSpan.FromMinutes(GatewaySessionSettleMinutes);

    /// <summary>Gets how old a pending gateway session must be to count as stalled, as a span.</summary>
    public TimeSpan GatewayStalledAfter => TimeSpan.FromHours(GatewayStalledAfterHours);

    /// <summary>
    /// Gets how long the reconciler waits before asking again about a session it has already asked
    /// about <paramref name="checks"/> times: the back-off start, doubled for every ask after the
    /// first, never longer than <see cref="GatewayCheckMaxIntervalMinutes"/>.
    /// </summary>
    /// <param name="checks">How many times the session has been asked about; at least one.</param>
    /// <returns>The wait before the next ask.</returns>
    public TimeSpan GatewayCheckInterval(int checks)
    {
        var minutes = (double)GatewayCheckBackoffStartMinutes;
        for (var ask = 1; ask < checks && minutes < GatewayCheckMaxIntervalMinutes; ask++)
        {
            minutes *= 2;
        }

        return TimeSpan.FromMinutes(Math.Min(minutes, GatewayCheckMaxIntervalMinutes));
    }

    /// <summary>Checks the relations between values that no single range can express.</summary>
    /// <param name="validationContext">The validation context; not used.</param>
    /// <returns>One failure per relation that does not hold, none otherwise.</returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // A cap below the start would make the first wait longer than the cap allows.
        if (GatewayCheckMaxIntervalMinutes < GatewayCheckBackoffStartMinutes)
        {
            yield return new ValidationResult(
                "GatewayCheckMaxIntervalMinutes must not be shorter than GatewayCheckBackoffStartMinutes.",
                [nameof(GatewayCheckMaxIntervalMinutes), nameof(GatewayCheckBackoffStartMinutes)]);
        }

        // A dispatch pass that made every delivery and waited out every timeout must still end before its
        // job lease does, or another instance would start a second pass over the same records.
        if (FulfilmentCallTimeout * FulfilmentBatchSize >= Lease)
        {
            yield return new ValidationResult(
                "FulfilmentBatchSize times FulfilmentCallTimeoutSeconds must be shorter than LeaseMinutes.",
                [nameof(FulfilmentBatchSize), nameof(FulfilmentCallTimeoutSeconds), nameof(LeaseMinutes)]);
        }

        // A job is abandoned only after it missed several beats; a threshold at or under two beats
        // would fail a healthy pull whose save was merely slow.
        if (MigrationStaleAfter <= MigrationHeartbeat * 2)
        {
            yield return new ValidationResult(
                "MigrationStaleAfterMinutes must be longer than two MigrationHeartbeatSeconds.",
                [nameof(MigrationStaleAfterMinutes), nameof(MigrationHeartbeatSeconds)]);
        }
    }
}
