namespace Innovayse.Application.Billing.Commands.PayMyInvoice;

using Innovayse.Application.Billing.Commands.PayInvoice;

/// <summary>Command for a client to pay one of their own invoices.</summary>
/// <remarks>
/// Carries no client id. Which account the invoice must belong to is resolved inside the handler
/// from the credential. The admin route that may settle any invoice dispatches
/// <see cref="PayInvoiceCommand"/> directly. It carries no currency either: the invoice is
/// charged in the currency it was issued in.
/// </remarks>
/// <param name="InvoiceId">The invoice to pay.</param>
public sealed record PayMyInvoiceCommand(int InvoiceId);
