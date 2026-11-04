namespace Innovayse.Hostpanel.Application.Billing.Commands.ChargeInvoiceStoredMethod;

/// <summary>
/// Charges what is still due on an invoice to the stored payment method its client consented to, through the payment
/// plugin that holds it. Dispatched by the system (the renewal run), never by a client, so it names an invoice and no
/// caller.
/// </summary>
/// <param name="InvoiceId">The invoice to charge.</param>
public sealed record ChargeInvoiceStoredMethodCommand(int InvoiceId);
