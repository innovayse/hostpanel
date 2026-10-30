namespace Innovayse.Hostpanel.Application.Billing.Commands.LapseOverdueInvoice;

/// <summary>Command to mark an overdue invoice lapsed once every digital renewal line on it is past its grace period.</summary>
/// <param name="InvoiceId">The invoice to lapse.</param>
public sealed record LapseOverdueInvoiceCommand(int InvoiceId);
