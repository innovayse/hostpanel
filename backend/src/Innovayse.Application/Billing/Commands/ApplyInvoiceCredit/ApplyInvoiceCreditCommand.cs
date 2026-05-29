namespace Innovayse.Application.Billing.Commands.ApplyInvoiceCredit;

/// <summary>Command to apply a credit amount to an invoice.</summary>
/// <param name="InvoiceId">The invoice to apply credit to.</param>
/// <param name="Amount">The credit amount to apply.</param>
public record ApplyInvoiceCreditCommand(int InvoiceId, decimal Amount);
