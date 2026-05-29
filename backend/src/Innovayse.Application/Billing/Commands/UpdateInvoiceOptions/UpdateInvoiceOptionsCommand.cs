namespace Innovayse.Application.Billing.Commands.UpdateInvoiceOptions;

/// <summary>Command to update invoice dates, payment method, and tax rate.</summary>
/// <param name="InvoiceId">The invoice to update.</param>
/// <param name="InvoiceDate">The new invoice issue date (UTC).</param>
/// <param name="DueDate">The new payment due date (UTC).</param>
/// <param name="PaymentMethod">Preferred payment method; null to clear.</param>
/// <param name="TaxRate">Tax rate percentage (0–100).</param>
public record UpdateInvoiceOptionsCommand(
    int InvoiceId,
    DateTimeOffset InvoiceDate,
    DateTimeOffset DueDate,
    string? PaymentMethod,
    decimal TaxRate);
