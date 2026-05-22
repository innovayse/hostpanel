namespace Innovayse.Application.Billing.Commands.ProcessBillableItemsCron;

/// <summary>
/// Scheduled command that processes billable items due for automatic invoicing.
/// Handles both one-time cron items and recurring items.
/// Dispatched daily by <c>BillingScheduledJobsStartup</c>.
/// </summary>
public record ProcessBillableItemsCronCommand;
