namespace Innovayse.Application.Billing.Commands.PublishInvoice;

/// <summary>Command to publish a draft invoice, making it payable.</summary>
/// <param name="InvoiceId">The draft invoice to publish.</param>
public record PublishInvoiceCommand(int InvoiceId);
