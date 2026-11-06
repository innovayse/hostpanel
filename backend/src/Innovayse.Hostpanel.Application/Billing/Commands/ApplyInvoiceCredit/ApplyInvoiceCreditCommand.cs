namespace Innovayse.Hostpanel.Application.Billing.Commands.ApplyInvoiceCredit;

/// <summary>Command to apply a credit amount to an invoice.</summary>
/// <param name="InvoiceId">The invoice to apply credit to.</param>
/// <param name="Amount">The credit amount to apply.</param>
/// <param name="SettleWhenCovered">
/// Whether credit that covers everything the invoice owes also settles it (marks it paid and fulfils its order). A client paying with
/// credit always means it; staff may apply credit as a step and finish the invoice themselves, so it is off unless asked for. A
/// part-paid invoice that the credit covers the rest of is settled either way.
/// </param>
public sealed record ApplyInvoiceCreditCommand(int InvoiceId, decimal Amount, bool SettleWhenCovered = false);
