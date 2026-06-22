namespace Innovayse.Application.Servers.Commands.SyncServerAccountCountsCron;

/// <summary>
/// Scheduled command to sync account counts from all active provisioning servers.
/// Dispatched daily at 06:00 UTC by <c>BillingScheduledJobsStartup</c>.
/// </summary>
public sealed record SyncServerAccountCountsCronCommand;
