namespace Innovayse.Application.Provisioning.Commands.AutoTerminateSuspendedCron;

/// <summary>
/// Scheduled command to auto-terminate services that have been suspended longer than the allowed limit.
/// Dispatched daily at 08:00 UTC by <c>BillingScheduledJobsStartup</c>.
/// </summary>
public sealed record AutoTerminateSuspendedCronCommand;
