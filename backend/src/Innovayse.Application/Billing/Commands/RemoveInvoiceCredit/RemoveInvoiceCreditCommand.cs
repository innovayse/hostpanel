namespace Innovayse.Application.Billing.Commands.RemoveInvoiceCredit;

/// <summary>Command to remove a specific credit amount from an invoice.</summary>
/// <param name="InvoiceId">The invoice to remove credit from.</param>
/// <param name="Amount">The credit amount to remove.</param>
public record RemoveInvoiceCreditCommand(int InvoiceId, decimal Amount);
