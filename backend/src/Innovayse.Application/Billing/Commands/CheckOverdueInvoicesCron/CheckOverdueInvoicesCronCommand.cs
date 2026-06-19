namespace Innovayse.Application.Billing.Commands.CheckOverdueInvoicesCron;

/// <summary>
/// Scheduled command to check for and mark overdue invoices.
/// Dispatched daily at 07:00 UTC by <c>BillingScheduledJobsStartup</c>.
/// </summary>
public record CheckOverdueInvoicesCronCommand;
