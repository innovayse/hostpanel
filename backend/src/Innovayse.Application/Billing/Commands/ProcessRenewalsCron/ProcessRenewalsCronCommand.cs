namespace Innovayse.Application.Billing.Commands.ProcessRenewalsCron;

/// <summary>
/// Scheduled command to generate renewal invoices for services due for renewal.
/// Dispatched daily at 05:00 UTC by <c>BillingScheduledJobsStartup</c>.
/// </summary>
public record ProcessRenewalsCronCommand;
