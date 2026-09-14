namespace Innovayse.Application.Billing.Commands.PayInvoice;

/// <summary>Command to charge the client's payment method and mark the invoice paid.</summary>
/// <remarks>
/// Carries no currency: an invoice is charged in the currency it was issued in, which the
/// handler reads from the invoice itself. A caller-supplied code could only ever agree with it
/// or be wrong.
/// </remarks>
/// <param name="InvoiceId">The invoice to pay.</param>
public record PayInvoiceCommand(int InvoiceId);
